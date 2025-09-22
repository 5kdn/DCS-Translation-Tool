using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Shared.Models;

using FluentResults;

namespace DcsTranslationTool.Application.Interfaces;

/// <summary>APIクライアントの機能をまとめる</summary>
public interface IApiService {
    /// <summary>APIのヘルス情報を取得する</summary>
    Task<Result<ApiHealth>> GetHealthAsync( CancellationToken cancellationToken = default );

    /// <summary>翻訳リポジトリのツリー一覧を取得する</summary>
    Task<Result<IReadOnlyList<FileEntry>>> GetTreeAsync( CancellationToken cancellationToken = default );

    /// <summary>指定パス群のZIPアーカイブをダウンロードする</summary>
    Task<Result<ApiDownloadFilesResult>> DownloadFilesAsync( ApiDownloadFilesRequest request, CancellationToken cancellationToken = default );

    /// <summary>APIを呼び出してPull Requestを作成する</summary>
    Task<Result<ApiCreatePullRequestOutcome>> CreatePullRequestAsync( ApiCreatePullRequestRequest request, CancellationToken cancellationToken = default );
}