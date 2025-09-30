using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

/// <summary>
/// <see cref="AppSettingsService"/> を検証するテストクラス
/// </summary>
public sealed class AppSettingsServiceTests : IDisposable {
    private readonly Mock<ILoggingService> logger = new();
    private readonly string tempDir;
    private readonly string settingsFileName = "appsettings.json";

    public AppSettingsServiceTests() {
        tempDir = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString( "N" ) );
        Directory.CreateDirectory( tempDir );
    }

    /// <summary>
    /// 使用したリソースを破棄する処理
    /// </summary>
    public void Dispose() {
        if(Directory.Exists( tempDir )) Directory.Delete( tempDir, recursive: true );
        GC.SuppressFinalize( this );
    }

    #region Constructor

    [Fact]
    public void Constructorでファイルが存在しないと既定値で初期化する() {
        // Arrange & Act
        using var sut = CreateService();

        // Assert
        Assert.NotNull( sut.Settings );
        Assert.False( File.Exists( Path.Combine( tempDir, settingsFileName ) ) );
    }

    [Fact]
    public void Constructorで本体が破損しバックアップが有効ならバックアップを読み込む() {
        // Arrange
        var path = Path.Combine(tempDir, settingsFileName);
        var bak = path + ".bak";

        File.WriteAllText( path, "<<<invalid json>>>" );
        File.WriteAllText( bak, "{}" );

        // Act
        using var sut = CreateService();

        // Assert
        Assert.NotNull( sut.Settings );
    }

    [Fact]
    public void Constructorで本体とバックアップが破損していると既定値で初期化する() {
        // Arrange & Act
        var path = Path.Combine(tempDir, settingsFileName);
        var bak = path + ".bak";
        File.WriteAllText( path, "<<<invalid json>>>" );
        File.WriteAllText( bak, "<<<invalid json>>>" );
        using var sut = CreateService();

        // Assert
        Assert.NotNull( sut.Settings );
    }

    #endregion

    #region SaveAsync

    [Fact]
    public async Task SaveAsyncを初回に実行するとファイルとバックアップを作成する() {
        // Arrange
        var path = Path.Combine(tempDir, settingsFileName);
        var bak = path + ".bak";
        await using var sut = CreateService();

        //Act
        await sut.SaveAsync( TestContext.Current.CancellationToken );

        // Assert
        Assert.True( File.Exists( path ) );
        Assert.True( File.Exists( bak ) );
        Assert.True( new FileInfo( path ).Length > 0 );
    }

    [Fact]
    public async Task SaveAsyncで保存先が存在しないとディレクトリを自動作成する() {
        // Arrange
        var root = Path.Combine(tempDir, "NonExistent", "Directory");
        var path = Path.Combine(root, settingsFileName);
        await using var sut = CreateService(root);

        // Act
        await sut.SaveAsync( TestContext.Current.CancellationToken );

        // Assert
        Assert.True( Directory.Exists( root ) );
        Assert.True( File.Exists( path ) );
    }

    [Fact]
    public async Task SaveAsyncを二回目以降に実行すると原子的置換でバックアップを維持する() {
        // Arrange
        var path = Path.Combine(tempDir, settingsFileName);
        var bak = path + ".bak";
        await using var sut = CreateService();

        // Act
        await sut.SaveAsync( TestContext.Current.CancellationToken );
        var firstWriteTime = File.GetLastWriteTimeUtc(path);
        var firstBakWriteTime = File.GetLastWriteTimeUtc(bak);

        await Task.Delay( 50, TestContext.Current.CancellationToken );
        await sut.SaveAsync( TestContext.Current.CancellationToken );

        var secondWriteTime = File.GetLastWriteTimeUtc(path);
        var secondBakWriteTime = File.GetLastWriteTimeUtc(bak);

        // Assert
        Assert.True( secondWriteTime >= firstWriteTime );
        Assert.True( File.Exists( bak ) );
        Assert.True( secondBakWriteTime >= firstBakWriteTime );
    }

    [Fact]
    public async Task SaveAsyncでキャンセル要求を受けると中断する() {
        // Arrange
        await using var sut = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>( () => sut.SaveAsync( cts.Token ) );
    }

    [Fact]
    public async Task SaveAsyncを同時に呼び出しても競合せずに完了する() {
        // Arrange
        var path = Path.Combine(tempDir, settingsFileName);
        await using var sut = CreateService();

        // Act
        var tasks = Enumerable.Range(0, 8).Select(_ => sut.SaveAsync()).ToArray();
        await Task.WhenAll( tasks );

        // Assert
        Assert.True( File.Exists( path ) );
        Assert.True( new FileInfo( path ).Length > 0 );
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsyncを呼び出すと保留中の保存をフラッシュする() {
        // Arrange
        var path = Path.Combine(tempDir, settingsFileName);
        await using var sut = CreateService();

        // Act & Assert
        Assert.False( File.Exists( path ) );

        var mi = typeof(AppSettingsService).GetMethod("RequestSave", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull( mi );

        mi!.Invoke( sut, null );

        await sut.DisposeAsync();
        Assert.True( File.Exists( path ) );
    }

    #endregion

    #region 自動保存トリガ（INotifyPropertyChanged / CollectionChanged）

    [Fact]
    public async Task AttachChangeObserversでINotifyPropertyChangedの変更を監視すると自動保存をトリガする() {
        // Arrange & Act
        var path = Path.Combine(tempDir, settingsFileName);
        var dummy = new DummyNotifying();
        const int timeoutMs = 2000;
        await using var sut = CreateService();
        InvokePrivate( sut, "AttachChangeObservers", dummy );

        // Assert
        // 変更前は未保存であること
        Assert.False( File.Exists( path ) );
        dummy.Name = "changed"; // PropertyChanged を発火

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while(sw.ElapsedMilliseconds < timeoutMs) {
            if(File.Exists( path ) && new FileInfo( path ).Length > 0) return;
            await Task.Delay( 50, TestContext.Current.CancellationToken );
        }
        Assert.True( File.Exists( path ), "ファイルが所定時間内に作成されなかった" );
        Assert.True( new FileInfo( path ).Length > 0, "ファイルサイズが0だった" );
    }

    [Fact]
    public async Task AttachChangeObserversでPropertyNameが空と通知されると子プロパティを購読して保存する() {
        // Arrange
        var path = Path.Combine(tempDir, settingsFileName);
        var parent = new ParentWithChild();
        const int timeoutMs = 2000;
        await using var sut = CreateService();
        InvokePrivate( sut, "AttachChangeObservers", parent );

        // Act & Assert
        // PropertyChanged(propertyName: null) を発火して深掘り購読させる
        parent.RaiseAllPropertiesChanged();
        // 子の変更で保存が走ることを確認
        parent.Child.Value = 123;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while(sw.ElapsedMilliseconds < timeoutMs) {
            if(File.Exists( path ) && new FileInfo( path ).Length > 0) return;
            await Task.Delay( 50, TestContext.Current.CancellationToken );
        }
        Assert.True( File.Exists( path ), "ファイルが所定時間内に作成されなかった" );
        Assert.True( new FileInfo( path ).Length > 0, "ファイルサイズが0だった" );
    }

    [Fact]
    public async Task AttachChangeObserversでINotifyCollectionChangedの追加を受けると新規要素を購読して保存する() {
        // Arrange
        var path = Path.Combine(tempDir, settingsFileName);
        var collection = new ObservableCollection<DummyNotifying>();
        var item = new DummyNotifying();
        const int timeoutMs = 2000;
        await using var sut = CreateService();
        InvokePrivate( sut, "AttachChangeObservers", collection );
        // Act & Assert
        collection.Add( item ); // 追加で RequestSave も走る
        // 追加だけでも保存が走るが、確実化のため変更も投げる
        item.Name = "after-add";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while(sw.ElapsedMilliseconds < timeoutMs) {
            if(File.Exists( path ) && new FileInfo( path ).Length > 0) return;
            await Task.Delay( 50, TestContext.Current.CancellationToken );
        }
        Assert.True( File.Exists( path ), "ファイルが所定時間内に作成されなかった" );
        Assert.True( new FileInfo( path ).Length > 0, "ファイルサイズが0だった" );
    }

    #endregion

    #region 購読の重複防止・解除

    [Fact]
    public void Attachで同一インスタンスを登録しても重複購読しない() {
        // Arrange
        var dummy = new DummyNotifying();
        using var sut = CreateService();

        // Act
        InvokePrivate( sut, "AttachChangeObservers", dummy );
        var count1 = ObservedCount(sut);
        InvokePrivate( sut, "AttachChangeObservers", dummy );
        var count2 = ObservedCount(sut);

        // Assert
        Assert.Equal( count1, count2 );
    }

    [Fact]
    public void Detachを呼び出すと購読を解除する() {
        // Arrange
        var dummy = new DummyNotifying();
        using var sut = CreateService();

        // Act & Assert
        InvokePrivate( sut, "AttachChangeObservers", dummy );
        Assert.True( IsObserved( sut, dummy ) );

        InvokePrivate( sut, "DetachChangeObservers", dummy );
        Assert.False( IsObserved( sut, dummy ) );
    }

    [Fact]
    public void AttachとDetachを大量要素に対して実行しても例外なく完了する() {
        // Arrange
        using var sut = CreateService();
        var baseline = ObservedCount(sut);

        // Act & Assert
        var many = Enumerable.Range(0, 2_000).Select(_ => new object()).ToList();
        InvokePrivate( sut, "AttachChangeObservers", many );

        // many(要素数) + コレクション自身の1件 が登録されているはず
        var actual = ObservedCount( sut );
        var expected = baseline + many.Count + 1;
        Assert.Equal( expected, actual );

        InvokePrivate( sut, "DetachChangeObservers", many );
        var afterDetach  = ObservedCount( sut );
        Assert.Equal( baseline, afterDetach );
    }

    #endregion

    #region 破棄の冪等性と競合

    [Fact]
    public async Task DisposeAsyncを複数回呼び出しても冪等である() {
        // Arrange & Act
        await using var sut = CreateService();

        // Assert
        await sut.DisposeAsync();
        await sut.DisposeAsync(); // 2回目も例外にならない
    }

    [Fact]
    public async Task DisposeAsyncを完了した後は保存要求を無視する() {
        // Arrange
        var path = Path.Combine( tempDir, settingsFileName );
        await using var sut = CreateService();

        // Act
        await sut.SaveAsync( TestContext.Current.CancellationToken );
        await sut.DisposeAsync(); // ここで最終フラッシュにより更新されうる

        var tDisposed = File.GetLastWriteTimeUtc( path );

        // 破棄後に保存要求（無視されるはず）
        InvokePrivate( sut, "RequestSave" );
        await Task.Delay( 400, TestContext.Current.CancellationToken );
        var tAfter = File.GetLastWriteTimeUtc( path );

        // Assert
        Assert.Equal( tDisposed, tAfter );
    }

    #endregion

    #region タイマのデバウンス

    [Fact]
    public async Task RequestSaveを短時間に連続呼び出すとバッチされて最終的に保存される() {
        // Arrange
        var path = Path.Combine(tempDir, settingsFileName);
        const int timeoutMs = 2000;
        await using var sut = CreateService();

        // Act
        // 連打
        for(var i = 0; i < 10; i++) InvokePrivate( sut, "RequestSave" );

        // Assert
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while(sw.ElapsedMilliseconds < timeoutMs) {
            if(File.Exists( path ) && new FileInfo( path ).Length > 0) return;
            await Task.Delay( 50, TestContext.Current.CancellationToken );
        }
        Assert.True( File.Exists( path ), "ファイルが所定時間内に作成されなかった" );
        Assert.True( new FileInfo( path ).Length > 0, "ファイルサイズが0だった" );
    }

    #endregion

    private AppSettingsService CreateService( string? directory = null, string? fileName = null ) {
        return new( logger.Object, directory ?? tempDir, fileName ?? settingsFileName );
    }

    // ========= ヘルパ ===========

    private static void InvokePrivate( object target, string methodName, params object?[]? args ) {
        var mi = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull( mi );
        mi!.Invoke( target, args );
    }

    private static int ObservedCount( object svc ) {
        var fi = svc.GetType().GetField("_observedObjects", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull( fi );
        var dict = (ConcurrentDictionary<object, byte>)fi!.GetValue(svc)!;
        return dict.Count;
    }

    private static bool IsObserved( object svc, object instance ) {
        var fi = svc.GetType().GetField("_observedObjects", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull( fi );
        var dict = (ConcurrentDictionary<object, byte>)fi!.GetValue(svc)!;
        return dict.ContainsKey( instance );
    }

    // ====== ダミー型 =======

    private sealed class DummyNotifying : INotifyPropertyChanged {
        private string name = string.Empty;

        public string Name {
            get => name;
            set {
                if(name == value) return;
                name = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Name ) ) );
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class ParentWithChild : INotifyPropertyChanged {
        public Child Child { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        // PropertyName が null/空 と等価にするため空文字で通知
        public void RaiseAllPropertiesChanged() =>
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( string.Empty ) );
    }

    private sealed class Child : INotifyPropertyChanged {
        private int value;

        public int Value {
            get => value;
            set {
                if(this.value == value) return;
                this.value = value;
                PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( nameof( Value ) ) );
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}