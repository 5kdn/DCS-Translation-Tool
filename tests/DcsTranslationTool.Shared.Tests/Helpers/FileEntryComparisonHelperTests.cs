using DcsTranslationTool.Shared.Helpers;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Shared.Tests.Helpers;

public class FileEntryComparisonHelperTests {
    #region Merge

    [Fact]
    public void Mergeはローカルとリポジトリのファイルを統合する() {
        // Arrange
        var local = new LocalFileEntry( "file.txt", "file.txt", false, "local-hash" );
        var repo = new RepoFileEntry( "file.txt", "file.txt", false, "repo-hash" );

        // Act
        var result = FileEntryComparisonHelper.Merge( [local], [repo] );

        // Assert
        Assert.Single( result );
        var actualEntry = result.First();
        Assert.Equal( "local-hash", actualEntry.LocalSha );
        Assert.Equal( "repo-hash", actualEntry.RepoSha );
    }

    [Fact]
    public void Mergeはローカルとリポジトリの別ファイルを識別する() {
        // Arrange
        var local = new LocalFileEntry( "local_only.txt", "local_only.txt", false, "local-hash" );
        var repo = new RepoFileEntry( "repo_only.txt", "repo_only.txt", false, "repo-hash" );

        // Act
        var result = FileEntryComparisonHelper.Merge( [local], [repo] );

        // Assert
        Assert.Equal( 2, result.Count() );
        var actualLocalEntry = result.Where(e=>e.Path=="local_only.txt");
        Assert.Single( actualLocalEntry );
        Assert.Equal( "local-hash", actualLocalEntry.First().LocalSha );
        Assert.Null( actualLocalEntry.First().RepoSha );

        var actualFileEntry = result.Where(e=>e.Path=="repo_only.txt");
        Assert.Single( actualFileEntry );
        Assert.Equal( "repo-hash", actualFileEntry.First().RepoSha );
        Assert.Null( actualFileEntry.First().LocalSha );
    }

    #endregion
}