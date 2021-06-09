using System;
using PdfDataExtractor;

namespace ToTestPdfDataExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            PDFDataExtractor pdf = new PDFDataExtractor(@"/Users/apple/Desktop/dd.pdf", "Mayank@123");
            Console.WriteLine($"{pdf.extract()}");
        }
    }
}
