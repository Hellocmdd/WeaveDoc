namespace WeaveDoc.Converter.Pandoc;

public interface IPdfConverter
{
    string Name { get; }

    void ConvertToPdf(string docxPath, string pdfPath);
}
