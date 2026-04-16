namespace WeaveDoc.Converter.Pandoc;

/// <summary>
/// 使用 Syncfusion DocIO 将 DOCX 转为 PDF，保留所有 OpenXML 样式
/// </summary>
public class SyncfusionPdfConverter
{
    public void ConvertToPdf(string docxPath, string pdfPath)
    {
        using var wordDoc = new Syncfusion.DocIO.DLS.WordDocument(docxPath);
        using var renderer = new Syncfusion.DocIORenderer.DocIORenderer();
        using var pdfDoc = renderer.ConvertToPDF(wordDoc);
        pdfDoc.Save(pdfPath);
    }
}
