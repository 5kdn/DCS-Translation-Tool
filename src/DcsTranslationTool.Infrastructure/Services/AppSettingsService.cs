
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// アプリケーション設定の読み書きを担うサービス。
/// </summary>
public sealed class AppSettingsService : IAppSettingsService, IDisposable, IAsyncDisposable {

    #region Fields

    private readonly ILoggingService _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    // 参照等価で重複購読防止。通常イベント購読なので明示的に解除する。
    private readonly ConcurrentDictionary<object, byte> _observedObjects = new(ReferenceEqualityComparer.Instance);
    // 反射メタデータの型キャッシュ
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    private readonly object _timerLock = new();
    private Timer? _saveTimer;
    private readonly CancellationTokenSource _shutdownCts = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
    private volatile Task _lastSaveTask = Task.CompletedTask;
    private bool _disposed;

    #endregion

    /// <summary>
    /// インスタンスを初期化して監視を開始する。
    /// </summary>
    /// <param name="logger">ロギングサービス。</param>
    /// <param name="saveDir">設定ファイルの保存ディレクトリ。</param>
    /// <param name="fileName">設定ファイル名。</param>
    public AppSettingsService( ILoggingService logger, string saveDir, string fileName ) {
        ArgumentNullException.ThrowIfNull( logger );
        ArgumentException.ThrowIfNullOrWhiteSpace( saveDir );
        ArgumentException.ThrowIfNullOrWhiteSpace( fileName );

        _logger = logger;

        if(!Directory.Exists( saveDir )) {
            Directory.CreateDirectory( saveDir );
            _logger.Debug( $"設定ディレクトリを作成した。Path={saveDir}" );
        }

        _filePath = Path.Combine( saveDir, fileName );
        _logger.Debug( $"設定ファイルのパスを初期化した。Path={_filePath}" );

        Settings = LoadSettings();
        AttachChangeObservers( Settings );
        _logger.Info( "アプリケーション設定を読み込んで監視を開始した。" );
    }

    #region Properties

    /// <summary>
    /// バックアップファイルのパス（常に現在の_filePathから導出する）
    /// </summary>
    private string BackupPath => _filePath + ".bak";

    /// <summary>
    /// 現在の設定値
    /// </summary>
    public AppSettings Settings { get; }

    #endregion

    public void Dispose() {
        // 同期Disposeは非同期版を待機してから最終リソース解放
        _logger.Debug( "AppSettingsService を同期破棄する。" );
        try {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally {
            _fileLock.Dispose();
            _shutdownCts.Dispose();
            _logger.Debug( "AppSettingsService の同期破棄が完了した。" );
        }
    }

    /// <summary>
    /// 非同期破棄。タイマ停止→保留保存のフラッシュ→購読解除。
    /// </summary>
    /// <returns>非同期タスク</returns>
    public async ValueTask DisposeAsync() {
        if(_disposed) return;
        _disposed = true;

        _logger.Debug( "AppSettingsService を非同期破棄する。" );

        // 以後のスケジュールを止める
        lock(_timerLock) {
            _saveTimer?.Dispose();
            _saveTimer = null;
        }

        // 進行中の保存があれば完了を待つ
        try { await _lastSaveTask.ConfigureAwait( false ); }
        catch(Exception ex) {
            _logger.Warn( "保留中の保存完了待機に失敗した。", ex );
        }

        // 最終フラッシュ
        try { await SaveAsync( _shutdownCts.Token ).ConfigureAwait( false ); }
        catch(Exception ex) {
            _logger.Error( "破棄処理中の設定保存に失敗した。", ex );
        }

        // 購読解除
        foreach(var obj in _observedObjects.Keys.ToArray()) {
            DetachChangeObservers( obj );
        }

        _logger.Debug( "AppSettingsService の非同期破棄が完了した。" );
    }

    #region Public Methods

    /// <inheritdoc />
    public async Task SaveAsync( CancellationToken cancellationToken = default ) {
        _logger.Debug( $"設定ファイルの保存を開始する。Path={_filePath}" );
        await _fileLock.WaitAsync( cancellationToken ).ConfigureAwait( false );
        try {
            var dir = Path.GetDirectoryName(_filePath);
            if(!string.IsNullOrEmpty( dir )) Directory.CreateDirectory( dir );

            // 同一ディレクトリに一時ファイルを作成し原子的に置換する
            var tempPath = Path.Combine(dir ?? Path.GetTempPath(), Path.GetRandomFileName());
            await using(var stream = File.Create( tempPath )) {
                // ソース生成メタデータを使用して高速・低アロケーションでシリアライズする
                await JsonSerializer
                    .SerializeAsync( stream, Settings, JsonOptions, cancellationToken )
                    .ConfigureAwait( false );

                await stream.FlushAsync( cancellationToken ).ConfigureAwait( false );
            }

            if(File.Exists( _filePath )) {                 // 既存がある場合はバックアップを作成して置換
                File.Replace( tempPath, _filePath, BackupPath, ignoreMetadataErrors: true );
            }
            else {
                File.Move( tempPath, _filePath );
                // バックアップも用意（失敗しても致命的ではない）
                try {
                    File.Copy( _filePath, BackupPath, overwrite: true );
                }
                catch(Exception ex) {
                    _logger.Warn( $"バックアップの作成に失敗した。BackupPath={BackupPath}", ex );
                }
            }

            _logger.Info( "設定ファイルの保存が完了した。" );
        }
        catch(Exception ex) {
            _logger.Error( "設定ファイルの保存に失敗した。", ex );
            throw;
        }
        finally {
            _fileLock.Release();
        }
    }

    #endregion

    #region Private Methods

    /// <summary>変更通知を購読し自動保存をトリガーする処理</summary>
    private void OnSettingsChanged( object? sender, PropertyChangedEventArgs e ) {
        if(sender is not null) {
            var type = sender.GetType();
            var props = GetCachedPublicInstanceProperties( type );

            // PropertyName が null/空文字の場合は全プロパティ変更として扱う
            if(string.IsNullOrEmpty( e.PropertyName )) {
                foreach(var p in props) {
                    if(p.GetIndexParameters().Length > 0) continue;
                    object? v;
                    try { v = p.GetValue( sender ); }
                    catch(Exception ex) {
                        _logger.Warn( $"{type.FullName}.{p.Name} の値取得に失敗した。", ex );
                        continue;
                    }
                    AttachChangeObservers( v );
                }
            }
            else {
                // 名前一致のプロパティのみ処理（キャッシュから線形検索）
                PropertyInfo? target = null;
                for(var i = 0; i < props.Length; i++) {
                    if(props[i].Name == e.PropertyName) { target = props[i]; break; }
                }
                if(target?.GetIndexParameters().Length == 0) {
                    object? v;
                    try { v = target.GetValue( sender ); }
                    catch(Exception ex) {
                        _logger.Warn( $"{type.FullName}.{target.Name} の値取得に失敗した。", ex );
                        v = null;
                    }
                    AttachChangeObservers( v );
                }
            }
        }

        RequestSave();
    }

    /// <summary>コレクション変更を購読し自動保存をトリガーする処理</summary>
    private void OnCollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
        if(e.NewItems is { Count: > 0 }) {
            foreach(var item in e.NewItems) {
                AttachChangeObservers( item );
            }
        }

        if(e.OldItems is { Count: > 0 }) {
            foreach(var item in e.OldItems) {
                DetachChangeObservers( item );
            }
        }

        RequestSave();
    }

    /// <summary>
    /// 保存処理を遅延呼び出しする処理
    /// </summary>
    private void RequestSave() {
        if(_disposed) return;

        lock(_timerLock) {
            _logger.Trace( "設定保存タイマーを起動する。" );
            // 非同期 void を避け、例外は握りつぶさない
            _saveTimer ??= new Timer( _ => {
                _lastSaveTask = Task.Run( async () => {
                    try { await SaveAsync().ConfigureAwait( false ); }
                    catch(Exception ex) {
                        _logger.Error( "遅延保存の実行に失敗した。", ex );
                    }
                } );
            } );
            _saveTimer.Change( TimeSpan.FromMilliseconds( 250 ), Timeout.InfiniteTimeSpan );
        }
    }

    /// <summary>
    /// 設定ファイルを読み込む。
    /// </summary>
    /// <returns>読み込んだ設定値</returns>
    private AppSettings LoadSettings() {
        if(!File.Exists( _filePath )) {
            _logger.Info( $"設定ファイルが存在しないため既定値を使用する。Path={_filePath}" );
            return new AppSettings();
        }
        try {
            using var stream = File.OpenRead(_filePath);
            var settings = JsonSerializer.Deserialize<AppSettings>( stream, JsonOptions );
            _logger.Info( "設定ファイルの読み込みに成功した。" );
            return settings ?? new AppSettings();
        }
        catch(Exception ex) {
            _logger.Warn( "設定ファイルの読み込みに失敗した。バックアップからの復旧を試みる。", ex );
            // 壊れたJSON時はバックアップからの復旧を試みる
            if(File.Exists( BackupPath )) {
                try {
                    using var stream = File.OpenRead(BackupPath);
                    var backup = JsonSerializer.Deserialize<AppSettings>( stream, JsonOptions );
                    _logger.Info( "バックアップから設定を復旧した。" );
                    return backup ?? new AppSettings();
                }
                catch(Exception backupEx) {
                    _logger.Error( "バックアップからの復旧に失敗した。", backupEx );
                }
            }
            _logger.Warn( "復旧に失敗したため既定値を返す。" );
            return new AppSettings();
        }
    }

    /// <summary>
    /// 変更通知を監視対象に追加する処理
    /// </summary>
    /// <param name="instance">監視対象オブジェクト</param>
    private void AttachChangeObservers( object? instance ) {
        if(instance is null) return;

        if(instance is IEnumerable enumerable and not string) {
            foreach(var item in enumerable) {
                AttachChangeObservers( item );
            }
        }

        // 参照等価で重複購読を防止
        var added = _observedObjects.TryAdd( instance, 0 );

        if(!added) return;

        // 通常イベント購読（Infrastructure から WPF 依存を排除）
        if(instance is INotifyPropertyChanged npc) {
            try {
                npc.PropertyChanged += OnSettingsChanged;
            }
            catch(Exception ex) {
                _logger.Error( $"PropertyChanged イベント購読の登録に失敗した。Type={instance.GetType().FullName}", ex );
            }
            // 初期探索は浅く開始し、変更時に深掘りする
            var props = GetCachedPublicInstanceProperties( instance.GetType() );
            foreach(var property in props) {
                if(property.GetIndexParameters().Length > 0) continue;
                object? value;
                try { value = property.GetValue( instance ); }
                catch(Exception ex) {
                    _logger.Warn( $"{instance.GetType().FullName}.{property.Name} の初期値取得に失敗した。", ex );
                    continue;
                }
                AttachChangeObservers( value );
            }
        }

        if(instance is INotifyCollectionChanged ncc) {
            try {
                ncc.CollectionChanged += OnCollectionChanged;
            }
            catch(Exception ex) {
                _logger.Error( $"CollectionChanged イベント購読の登録に失敗した。Type={instance.GetType().FullName}", ex );
            }
        }
    }

    /// <summary>
    /// 変更通知の購読を解除する処理
    /// </summary>
    /// <param name="instance">対象オブジェクト</param>
    private void DetachChangeObservers( object? instance ) {
        if(instance is null) return;

        if(_observedObjects.TryRemove( instance, out _ )) {
            if(instance is INotifyPropertyChanged npc) {
                try { npc.PropertyChanged -= OnSettingsChanged; }
                catch(Exception ex) {
                    _logger.Warn( $"PropertyChanged イベント購読の解除に失敗した。Type={instance.GetType().FullName}", ex );
                }
            }
            if(instance is INotifyCollectionChanged ncc) {
                try { ncc.CollectionChanged -= OnCollectionChanged; }
                catch(Exception ex) {
                    _logger.Warn( $"CollectionChanged イベント購読の解除に失敗した。Type={instance.GetType().FullName}", ex );
                }
            }
        }

        if(instance is IEnumerable enumerable and not string) {
            foreach(var item in enumerable) {
                DetachChangeObservers( item );
            }
        }
    }

    /// <summary>
    /// 公開インスタンスプロパティ配列を型ごとにキャッシュして取得する。
    /// </summary>
    /// <param name="type">対象型</param>
    /// <returns>プロパティ配列</returns>
    private static PropertyInfo[] GetCachedPublicInstanceProperties( Type type ) {
        if(_propertyCache.TryGetValue( type, out var cached )) return cached;
        // 継承階層を含めた公開インスタンスプロパティを取得
        var props = type.GetProperties( BindingFlags.Instance | BindingFlags.Public );
        _propertyCache[type] = props;
        return props;
    }

    #endregion
}