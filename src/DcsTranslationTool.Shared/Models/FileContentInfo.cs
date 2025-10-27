using System.Text;

namespace DcsTranslationTool.Shared.Models;
/// <summary>
/// 解析結果を表すレコード。
/// </summary>
/// <param name="IsBinary">バイナリと判定された場合は true。</param>
/// <param name="Encoding">テキストと判定された場合のエンコード。バイナリ時は null。</param>
/// <param name="DetectionConfidence">エンコード推測の信頼度（0.0〜1.0）。</param>
/// <param name="Text">テキストと判定された場合のデコード済み文字列。バイナリ時は null。</param>
/// <param name="ByteCount">処理した総バイト数。</param>
public sealed record FileContentInfo(
    bool IsBinary,
    Encoding? Encoding,
    double DetectionConfidence,
    string? Text,
    int ByteCount );