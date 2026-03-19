using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using PptxMerger.Models;
using P  = DocumentFormat.OpenXml.Presentation;
using D  = DocumentFormat.OpenXml.Drawing;
using IO = System.IO;

namespace PptxMerger.Services;

public class PptxMergeService
{
    public event Action<string>? LogMessage;
    private void Log(string msg) => LogMessage?.Invoke(msg);

    // ─── 공개 진입점 ────────────────────────────────────────────────────────
    public void Merge(IList<string> sourcePaths, string outputPath, FormatConfig fmt)
    {
        if (sourcePaths.Count == 0) throw new ArgumentException("파일이 선택되지 않았습니다.");

        // 첫 번째 파일을 기반 템플릿으로 복사
        IO.File.Copy(sourcePaths[0], outputPath, overwrite: true);

        using var destDoc = PresentationDocument.Open(outputPath, isEditable: true);
        var destPrs = destDoc.PresentationPart!;

        // 기존 슬라이드 모두 제거
        ClearAllSlides(destPrs);

        foreach (var srcPath in sourcePaths)
        {
            Log($"▶ {IO.Path.GetFileName(srcPath)}");
            try
            {
                using var srcDoc = PresentationDocument.Open(srcPath, isEditable: false);
                var srcPrs = srcDoc.PresentationPart!;
                int count = srcPrs.Presentation.SlideIdList!.Elements<SlideId>().Count();

                for (int i = 0; i < count; i++)
                {
                    CopySlide(srcPrs, i, destPrs);
                    Log($"  슬라이드 {i + 1} 복사 완료");
                }
            }
            catch (Exception ex)
            {
                Log($"  [오류] {ex.Message}");
            }
        }

        // 서식 적용
        Log("서식 적용 중...");
        ApplyFormatToAll(destPrs, fmt);

        destPrs.Presentation.Save();
        Log($"저장 완료 → {outputPath}");
    }

    // ─── 슬라이드 전체 삭제 ─────────────────────────────────────────────────
    private static void ClearAllSlides(PresentationPart prsPart)
    {
        var slideIds = prsPart.Presentation.SlideIdList!.Elements<SlideId>().ToList();
        foreach (var slideId in slideIds)
        {
            var slidePart = (SlidePart)prsPart.GetPartById(slideId.RelationshipId!);
            prsPart.DeletePart(slidePart);
        }
        prsPart.Presentation.SlideIdList!.RemoveAllChildren<SlideId>();
    }

    // ─── 슬라이드 1장 복사 ──────────────────────────────────────────────────
    private static void CopySlide(PresentationPart srcPrs, int slideIndex, PresentationPart destPrs)
    {
        var srcSlideId = srcPrs.Presentation.SlideIdList!
            .Elements<SlideId>().ElementAt(slideIndex);
        var srcSlidePart = (SlidePart)srcPrs.GetPartById(srcSlideId.RelationshipId!);

        // 새 슬라이드 파트 추가
        var destSlidePart = destPrs.AddNewPart<SlidePart>();

        // 슬라이드 XML 복사
        using (var s = srcSlidePart.GetStream())
            destSlidePart.FeedData(s);

        // 이미지 파트 복사 (원본 RelationshipId 유지 → XML 참조 그대로 유효)
        foreach (var partRef in srcSlidePart.Parts)
        {
            if (partRef.OpenXmlPart is ImagePart srcImg)
            {
                var destImg = destSlidePart.AddNewPart<ImagePart>(
                    srcImg.ContentType, partRef.RelationshipId);
                using var imgStream = srcImg.GetStream();
                destImg.FeedData(imgStream);
            }
        }

        // 레이아웃 연결 — 원본 레이아웃 rId 그대로, 대상 레이아웃 파트 사용
        var srcLayoutRel = srcSlidePart.SlideLayoutPart!;
        var srcLayoutRelId = srcSlidePart.GetIdOfPart(srcLayoutRel);
        var destLayout = destPrs.SlideMasterParts.First().SlideLayoutParts.First();
        destSlidePart.AddPart(destLayout, srcLayoutRelId);

        // 슬라이드 목록에 등록
        var slideList = destPrs.Presentation.SlideIdList!;
        uint maxId = slideList.Elements<SlideId>().Any()
            ? slideList.Elements<SlideId>().Max(s => s.Id!.Value)
            : 255u;

        slideList.Append(new SlideId
        {
            Id = maxId + 1,
            RelationshipId = destPrs.GetIdOfPart(destSlidePart)
        });
    }

    // ─── 전체 서식 적용 ─────────────────────────────────────────────────────
    private static void ApplyFormatToAll(PresentationPart prsPart, FormatConfig fmt)
    {
        var slideIds = prsPart.Presentation.SlideIdList!.Elements<SlideId>().ToList();
        foreach (var slideId in slideIds)
        {
            var slidePart = (SlidePart)prsPart.GetPartById(slideId.RelationshipId!);
            ApplyFormatToSlide(slidePart.Slide, fmt);
        }
    }

    private static void ApplyFormatToSlide(Slide slide, FormatConfig fmt)
    {
        // 일반 도형의 텍스트 프레임
        foreach (var txBody in slide.Descendants<P.TextBody>())
            ApplyFormatToTextBody(txBody, fmt);

        // 표(Table) 셀
        foreach (var cell in slide.Descendants<D.TableCell>())
            if (cell.TextBody != null)
                ApplyFormatToTextBody(cell.TextBody, fmt);
    }

    private static void ApplyFormatToTextBody(OpenXmlElement txBody, FormatConfig fmt)
    {
        var align = fmt.Align switch
        {
            "center"  => D.TextAlignmentTypeValues.Center,
            "right"   => D.TextAlignmentTypeValues.Right,
            "justify" => D.TextAlignmentTypeValues.Justified,
            _         => D.TextAlignmentTypeValues.Left,
        };

        foreach (var para in txBody.Elements<D.Paragraph>())
        {
            // 단락 속성
            var pPr = para.ParagraphProperties
                ?? para.PrependChild(new D.ParagraphProperties());
            pPr.Alignment = align;
            pPr.LineSpacing = new D.LineSpacing(
                new D.SpacingPercent { Val = (int)(fmt.LineSpacing * 100000) });

            // 런 속성
            foreach (var run in para.Elements<D.Run>())
            {
                var rPr = run.RunProperties
                    ?? run.PrependChild(new D.RunProperties());

                rPr.FontSize = (int)(fmt.FontSizePt * 100);
                rPr.Bold = fmt.Bold ? true : (bool?)null;

                if (fmt.CharSpacing != 0.0)
                    rPr.Spacing = (int)(fmt.CharSpacing * 100);

                // 폰트
                rPr.RemoveAllChildren<D.LatinFont>();
                rPr.Append(new D.LatinFont { Typeface = fmt.FontName });

                // 색상
                rPr.RemoveAllChildren<D.SolidFill>();
                rPr.RemoveAllChildren<D.GradientFill>();
                rPr.RemoveAllChildren<D.NoFill>();
                rPr.Append(new D.SolidFill(
                    new D.RgbColorModelHex { Val = fmt.ColorHex.TrimStart('#') }));
            }
        }
    }
}
