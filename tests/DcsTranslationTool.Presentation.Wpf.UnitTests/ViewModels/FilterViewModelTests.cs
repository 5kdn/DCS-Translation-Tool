using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.ViewModels;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.ViewModels;

/// <summary>
/// FilterViewModel のフィルタ状態遷移と通知動作を検証する。
/// </summary>
public sealed class FilterViewModelTests {
    /// <summary>
    /// 初期状態ですべてのフィルタが有効であることを検証する。
    /// </summary>
    [Fact]
    public void 初期状態では全フィルタが有効である() {
        var viewModel = CreateViewModel();

        Assert.True( viewModel.All );
        Assert.True( viewModel.Unchanged );
        Assert.True( viewModel.RepoOnly );
        Assert.True( viewModel.LocalOnly );
        Assert.True( viewModel.Modified );
        Assert.Collection(
            viewModel.GetActiveTypes(),
            type => Assert.Equal( FileChangeType.Unchanged, type ),
            type => Assert.Equal( FileChangeType.RepoOnly, type ),
            type => Assert.Equal( FileChangeType.LocalOnly, type ),
            type => Assert.Equal( FileChangeType.Modified, type ) );
    }

    /// <summary>
    /// All を無効化した際に個別フィルタがすべて無効化されることを検証する。
    /// </summary>
    [Fact]
    public void Allをfalseにすると個別フィルタがすべてfalseになる() {
        var viewModel = CreateViewModel();
        var filtersChangedCount = 0;
        viewModel.FiltersChanged += ( _, _ ) => filtersChangedCount++;

        viewModel.All = false;

        Assert.False( viewModel.All );
        Assert.False( viewModel.Unchanged );
        Assert.False( viewModel.RepoOnly );
        Assert.False( viewModel.LocalOnly );
        Assert.False( viewModel.Modified );
        Assert.Empty( viewModel.GetActiveTypes() );
        Assert.Equal( 5, filtersChangedCount );
    }

    /// <summary>
    /// All を有効化した際に個別フィルタがすべて有効化されることを検証する。
    /// </summary>
    [Fact]
    public void Allをtrueにすると個別フィルタがすべてtrueになる() {
        var viewModel = CreateViewModel();
        viewModel.All = false;

        var filtersChangedCount = 0;
        viewModel.FiltersChanged += ( _, _ ) => filtersChangedCount++;

        viewModel.All = true;

        Assert.True( viewModel.All );
        Assert.True( viewModel.Unchanged );
        Assert.True( viewModel.RepoOnly );
        Assert.True( viewModel.LocalOnly );
        Assert.True( viewModel.Modified );
        Assert.Collection(
            viewModel.GetActiveTypes(),
            type => Assert.Equal( FileChangeType.Unchanged, type ),
            type => Assert.Equal( FileChangeType.RepoOnly, type ),
            type => Assert.Equal( FileChangeType.LocalOnly, type ),
            type => Assert.Equal( FileChangeType.Modified, type ) );
        Assert.Equal( 5, filtersChangedCount );
    }

    /// <summary>
    /// 個別フィルタ変更時に All が再計算されることを検証する。
    /// </summary>
    [Fact]
    public void 個別フィルタ変更時にAllが再計算される() {
        var viewModel = CreateViewModel();
        var filtersChangedCount = 0;
        viewModel.FiltersChanged += ( _, _ ) => filtersChangedCount++;

        viewModel.Unchanged = false;
        viewModel.Unchanged = true;
        viewModel.RepoOnly = false;
        viewModel.RepoOnly = true;
        viewModel.LocalOnly = false;
        viewModel.LocalOnly = true;
        viewModel.Modified = false;
        viewModel.Modified = true;

        Assert.True( viewModel.All );
        Assert.True( viewModel.Unchanged );
        Assert.True( viewModel.RepoOnly );
        Assert.True( viewModel.LocalOnly );
        Assert.True( viewModel.Modified );
        Assert.Equal( 8, filtersChangedCount );
    }

    /// <summary>
    /// 個別フィルタ状態に応じて有効種別のみ列挙することを検証する。
    /// </summary>
    [Fact]
    public void GetActiveTypesは有効な種別のみを列挙する() {
        var viewModel = CreateViewModel();

        viewModel.Unchanged = false;
        viewModel.LocalOnly = false;

        Assert.False( viewModel.All );
        Assert.Collection(
            viewModel.GetActiveTypes(),
            type => Assert.Equal( FileChangeType.RepoOnly, type ),
            type => Assert.Equal( FileChangeType.Modified, type ) );
    }

    /// <summary>
    /// すべての個別フィルタを無効化した場合に空列挙を返すことを検証する。
    /// </summary>
    [Fact]
    public void すべての個別フィルタがfalseの場合は空列挙を返す() {
        var viewModel = CreateViewModel();

        viewModel.Unchanged = false;
        viewModel.RepoOnly = false;
        viewModel.LocalOnly = false;
        viewModel.Modified = false;

        Assert.False( viewModel.All );
        Assert.Empty( viewModel.GetActiveTypes() );
    }

    /// <summary>
    /// テスト対象 ViewModel を生成する。
    /// </summary>
    /// <returns>生成した <see cref="FilterViewModel"/>。</returns>
    private static FilterViewModel CreateViewModel() {
        var loggerMock = new Mock<ILoggingService>();
        return new FilterViewModel( loggerMock.Object );
    }
}