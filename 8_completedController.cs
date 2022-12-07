using Microsoft.AspNetCore.Mvc;
using GrapeCity.Documents.Pdf;
using GrapeCity.Documents.Common;
using GrapeCity.Documents.Pdf.Annotations;
using GrapeCity.Documents.Drawing;
using Amazon.S3.Transfer;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO.Compression;
using System.Drawing;
using System.Text;
using System.Net;
using System.Xml;


namespace GrapeCityDocsAWSLambda.Controllers;

// MergePdfs CLASS DEFINES INPUT PARAMETERS - KEY & VALUE
public class MergePdfs
{
    public string? Key1 { get; set; }
    public string? Key2 { get; set; }
}

// SplitPdfs CLASS DEFINES INPUT PARAMETERS - KEY & VALUE
public class SplitPdfs
{
    public string? Key1 { get; set; }
    public int Key2 { get; set; }
}

// WatermarkPdf CLASS DEFINES INPUT PARAMETERS - KEY & VALUE
public class WatermarkPdf
{
    public string? Key1 { get; set; }
    public string? Key2 { get; set; }
}

// Convert CLASS DEFINES INPUT PARAMETERS - KEY & VALUE
public class Convert
{
    public string? Key1 { get; set; }
    public string? Key2 { get; set; }
}

[Route("api/[controller]")]
public class ValuesController : ControllerBase
{

    // POST api/values/upload
    [HttpPost("upload")]
    public async Task<IActionResult> OnPostUploadAsync([FromForm] List<IFormFile> files)
    {

        // INITIALIZE AWS S3 SDK CREDENTIALS, REGION, CLIENT
        var credentials = new BasicAWSCredentials("MYACCESSKEY", "MYSECRETKEY");
        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName("us-east-2")
        };
        using var client = new AmazonS3Client(credentials, config);

        // CHECK HOW MANY ITEMS WERE UPLOADED
        long size = files.Sum(f => f.Length);
        Console.WriteLine("Hey: " + size);


        // AS LONG AS SOMETHING WAS UPLOADED BY CLIENT IN FORM (POSTMAN)
        // LOOP THROUGH THE FILES ARRAY AND UPLOAD EACH TO S3 BUCKET "gcpdf"
        foreach (var formFile in files)
        {
            if (formFile.Length > 0)
            {
                var filePath = Path.GetTempFileName();

                using (var stream = System.IO.File.Create(filePath))
                {
                    await formFile.CopyToAsync(stream);

                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        InputStream = stream,
                        Key = formFile.FileName,
                        BucketName = "gcpdf",
                        CannedACL = S3CannedACL.PublicRead
                    };

                    var fileTransferUtility = new TransferUtility(client);
                    await fileTransferUtility.UploadAsync(uploadRequest);
                }


            }
        }

        // PROCESS UPLOADED FILES
        // DO NOT RELY OR TRUST THE FileName property WITHOUT VALIDATION

        return Ok(new { count = files.Count, size });
    }

    
    // POST api/values/merge/pdf
    [HttpPost("merge/pdf")]
    public async Task<IEnumerable<string>> PostAsync([FromQuery] MergePdfs key)
    {
        // MAKING SURE INPUT PARAMATERS ARE NOT NULL OR EMPTY
        if (string.IsNullOrEmpty(key.Key1) || string.IsNullOrEmpty(key.Key2))
        {
            return new string[] { "Bad request!", "Input should NOT be empty!" };
        }

        else // CONTINUE
        {
            // RETRIEVE NAME OF UPLOADED PDF
            string upload1 = key.Key1;
            string upload2 = key.Key2;

            // INITIALIZE AWS S3 SDK CREDENTIALS, REGION, CLIENT
            var credentials = new BasicAWSCredentials("MYACCESSKEY", "MYSECRETKEY");
            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName("us-east-2")
            };
            using var client = new AmazonS3Client(credentials, config);

            // INITIALIZE THE TRANSFERUTILITY OBJECT WHICH DOES UPLOAD & DOWNLOAD
            TransferUtility fileTransferUtility = new TransferUtility(client);

            string pathway = Path.GetTempPath();

            // DEFINE S3 DOWNLOAD REQUEST
            var downloadRequest1 = new TransferUtilityDownloadRequest()
            {
                FilePath = pathway + upload1,
                BucketName = "gcpdf",
                Key = upload1

            };
            var downloadRequest2 = new TransferUtilityDownloadRequest()
            {
                FilePath = pathway + upload2,
                BucketName = "gcpdf",
                Key = upload2

            };

            // DOWNLOAD THE TWO(2) PDFs ASYNCHRONOUSLY, WAIT TILL COMPLETION
            await fileTransferUtility.DownloadAsync(downloadRequest1);
            await fileTransferUtility.DownloadAsync(downloadRequest2);


            // Create Pdf Document
            GcPdfDocument pdf1 = new GcPdfDocument();
            var fsone = new FileStream(Path.Combine(pathway + upload1), FileMode.Open, FileAccess.Read);
            //Load the document
            pdf1.Load(fsone);

            // Create Pdf Document
            GcPdfDocument pdf2 = new GcPdfDocument();
            var fstwo = new FileStream(Path.Combine(pathway + upload2), FileMode.Open, FileAccess.Read);
            //Load the document
            pdf2.Load(fstwo);

            pdf1.MergeWithDocument(pdf2, new MergeDocumentOptions());

            // Save PDF Locally, Rootpath
            pdf1.Save(pathway + "mergedInvoice.pdf");
            string path = pathway + "mergedInvoice.pdf";


            // UPLOAD MERGED PDF TO S3 BUCKET
            var stream = new MemoryStream(System.IO.File.ReadAllBytes(path));
            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = "mergedInvoice.pdf",
                BucketName = "gcpdf",
                CannedACL = S3CannedACL.PublicRead
            };
            await fileTransferUtility.UploadAsync(uploadRequest);


            // RETURN DOWNLOAD S3 URL TO MERGED PDF
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = "gcpdf",
                Key = "mergedInvoice.pdf",
                Expires = DateTime.Now.AddMinutes(5)
            };

            string outputt = "filelink: " + client.GetPreSignedURL(request);
            return new string[] { "Pdf merged", outputt };
        }

    }

    
    // GET api/values/splitpdfbypage
    [HttpGet("splitpdfbypage")]
    public async Task<IEnumerable<string>> GetAsync([FromQuery] SplitPdfs key)
    {

        // VALIDATING INPUT
        if (string.IsNullOrEmpty(key.Key1) || (key.Key2 < 1))
        {
            return new string[] { "Bad request!", "Input should NOT be empty!" };
        }

        else // CONTINUE
        {
            // RETRIEVE QUERY/PARAMs INPUTS i.e NAME OF UPLOADED PDF TO S3 AND NUMBER OF PAGES
            string upload = key.Key1;
            int noOfPages = key.Key2;

            // INITIALIZE AWS S3 SDK CREDENTIALS, REGION, CLIENT
            var credentials = new BasicAWSCredentials("MYACCESSKEY", "MYSECRETKEY");
            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName("us-east-2") // CHANGE REGION TO YOURS
            };
            using var client = new AmazonS3Client(credentials, config);

            // INITIALIZE THE S3 TRANSFERUTILITY OBJECT WHICH DOES UPLOAD & DOWNLOAD
            TransferUtility fileTransferUtility = new TransferUtility(client);

            // https://learn.microsoft.com/en-us/dotnet/api/system.io.path.gettemppath?view=net-7.0&tabs=windows#remarks
            string pathway = Path.GetTempPath();

            // DEFINE S3 DOWNLOAD REQUEST
            var downloadRequest = new TransferUtilityDownloadRequest()
            {
                FilePath = pathway + upload,
                BucketName = "gcpdf",
                Key = upload

            };

            // DOWNLOAD THE PDF ARTIFACT ASYNCHRONOUSLY, WAIT TILL COMPLETION
            await fileTransferUtility.DownloadAsync(downloadRequest);

            // CREATE SOURCE PDF OBJECT
            GcPdfDocument pdf = new();
            var fs = new FileStream(Path.Combine(pathway + upload), FileMode.Open, FileAccess.Read);

            // LOAD SOURCE i.e S3 "UPLOAD" PDF
            pdf.Load(fs);


            // CREATE NEW PDF OBJECTS
            GcPdfDocument pdf1 = new();
            GcPdfDocument pdf2 = new();
            GcPdfDocument pdf3 = new();

            // BASED ON THE NUMBER OF PAGES OF SOURCE PDF, MERGE EACH PAGE OF SOURCE TO A NEW PDF, IN CHRONOLOGY
            for (int i = 0, pageNumber = i + 1; i < noOfPages; i++, pageNumber++)
            {


                OutputRange pageRange = new OutputRange(pageNumber, pageNumber);

                if (i == 0)
                {
                    pdf1.MergeWithDocument(pdf, new MergeDocumentOptions()
                    {
                        PagesRange = pageRange // (1,1)
                    });
                    pdf1.Save(pathway + "page1.pdf");
                }

                if (i == 1)
                {
                    pdf2.MergeWithDocument(pdf, new MergeDocumentOptions()
                    {
                        PagesRange = pageRange // (2,2)
                    });
                    pdf2.Save(pathway + "page2.pdf");
                }


                if (i == 2)
                {
                    pdf3.MergeWithDocument(pdf, new MergeDocumentOptions()
                    {
                        PagesRange = pageRange // (3,3)
                    });
                    pdf3.Save(pathway + "page3.pdf");
                }

            }

            // FULLPATH TO ZIP
            string path = pathway + "SplitPDFByPage.zip";

            // OBTAIN ARRAY OF ALL FILES WITH REGEX "page*.pdf" e.g page1.pdf, page2.pdf, page3.pdf
            var docFiles = Directory.GetFiles(pathway, "page*.pdf");

            // DELETE ZIP FILE IF IT ALREADY EXISTS.
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }

            // OPEN ARCHIVE
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create, Encoding.UTF8);

            // LOOP THROUGH EACH SPLIT PAGE AND COMPRESS TO "SplitPDFByPage.zip" ARCHIVE
            foreach (var docFile in docFiles)
            {
                var entry =
                    archive.CreateEntryFromFile(
                        docFile,
                        Path.GetFileName(docFile),
                        CompressionLevel.Optimal
                    );

                Console.WriteLine($"{entry.FullName} was compressed.");
            }

            // CLOSE ZIP ARCHIVE
            archive.Dispose();


            // UPLOAD ZIP OF SPLIT PDFs TO S3 BUCKET
            var stream = new MemoryStream(System.IO.File.ReadAllBytes(path))
            {
                Position = 0
            };

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = "SplitPDFByPage.zip",
                BucketName = "gcpdf",
                CannedACL = S3CannedACL.PublicRead
            };

            await fileTransferUtility.UploadAsync(uploadRequest);

            // RETURN DOWNLOAD S3 URL OF ZIP CONTAINING SPLIT PDFs
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = "gcpdf",
                Key = "SplitPDFByPage.zip",
                Expires = DateTime.Now.AddMinutes(5)
            };

            string outputt = "filelink: " + client.GetPreSignedURL(request);
            return new string[] { "Pdf split to: " + noOfPages, outputt };

        }
    }

    
    // PUT api/values/addwatermark/5
    [HttpPut("addwatermark/{id}")]
    public async Task<IEnumerable<string>> PutAsync(int id, [FromQuery] WatermarkPdf key)
    {
        // VALIDATING INPUT
        if (string.IsNullOrEmpty(key.Key1) || string.IsNullOrEmpty(key.Key2))
        {
            return new string[] { "Bad request!", "Input should NOT be empty!" };
        }
        else // Continue
        {
            // RETRIEVE QUERY/PARAMs INPUTS i.e NAME OF UPLOADED PDF TO S3 AND NUMBER OF PAGES
            string? pdfDoc = key.Key1;
            string? watermark = key.Key2;

            // INITIALIZE AWS S3 SDK CREDENTIALS, REGION, CLIENT
            var credentials = new BasicAWSCredentials("MYACCESSKEY", "MYSECRETKEY");
            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName("us-east-2")
            };
            using var client = new AmazonS3Client(credentials, config);

            // INITIALIZE THE S3 TRANSFERUTILITY OBJECT WHICH DOES UPLOAD & DOWNLOAD
            TransferUtility fileTransferUtility = new TransferUtility(client);

            // https://learn.microsoft.com/en-us/dotnet/api/system.io.path.gettemppath?view=net-7.0&tabs=windows#remarks
            string pathway = Path.GetTempPath();

            // DEFINE S3 DOWNLOAD REQUEST
            var downloadRequest1 = new TransferUtilityDownloadRequest()
            {
                FilePath = pathway + pdfDoc,
                BucketName = "gcpdf",
                Key = pdfDoc

            };
            var downloadRequest2 = new TransferUtilityDownloadRequest()
            {
                FilePath = pathway + watermark,
                BucketName = "gcpdf",
                Key = watermark

            };

            // DOWNLOAD PDF AND WATERMARK ARTIFACT ASYNCHRONOUSLY, WAIT TILL COMPLETION
            await fileTransferUtility.DownloadAsync(downloadRequest1);
            await fileTransferUtility.DownloadAsync(downloadRequest2);

            // CREATE SOURCE PDF OBJECT
            var pdf = new GcPdfDocument();
            var fs = new FileStream(Path.Combine(pathway + pdfDoc), FileMode.Open, FileAccess.Read);
            // LOAD SOURCE i.e S3 "pdfDoc" PDF
            pdf.Load(fs);

            // LOOP THROUGH EACH PAGE OF THE SOURCE PDF AND ADD A WATERMARK
            foreach (var page in pdf.Pages)
            {
                // CREATE AN INSTANCE OF WatermarkAnnotation CLASS
                // SET RELEVANT PROPERTIES
                _ = new WatermarkAnnotation()
                {
                    Rect = new RectangleF(420, 23, 130, 50),
                    Image = Image.FromFile(pathway + watermark),
                    Page = page // Add watermark to page
                };
                // Save PDF Locally, Rootpath
                pdf.Save(pathway + "Wetlands-Watermarked.pdf");
            }

            // UPLOAD WATERMARKED PDF TO S3 BUCKET
            string path = pathway + "Wetlands-Watermarked.pdf";
            var stream = new MemoryStream(System.IO.File.ReadAllBytes(path));

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = "Wetlands-Watermarked.pdf",
                BucketName = "gcpdf",
                CannedACL = S3CannedACL.PublicRead
            };
            await fileTransferUtility.UploadAsync(uploadRequest);


            // RETURN DOWNLOAD S3 URL OF WATERMARKED PDF
            GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
            {
                BucketName = "gcpdf",
                Key = "Wetlands-Watermarked.pdf",
                Expires = DateTime.Now.AddMinutes(5)
            };

            string outputt = "filelink: " + client.GetPreSignedURL(request);
            return new string[] { "Pdf Watermarked!", outputt };

        }

    }


    // GET api/values/convert/format
    [HttpGet("convert/format")]
    public async Task<IEnumerable<string>> GetAsync(int id, [FromQuery] Convert key)
    {
        if (string.IsNullOrEmpty(key.Key1) || string.IsNullOrEmpty(key.Key2))
        {
            return new string[] { "Bad request!", "Input should NOT be empty!" };
        }

        else
        {
            // RETRIEVE QUERY/PARAMs INPUTS i.e NAME OF UPLOADED PDF TO S3 AND CONVERSION TYPE
            string? upload = key.Key1;
            string? format = key.Key2;


            // Render a PDF page to .svg 
            if (format == "pdf2svg")
            {

                // INITIALIZE AWS S3 SDK CREDENTIALS, REGION, CLIENT
                var credentials = new BasicAWSCredentials("MYACCESSKEY", "MYSECRETKEY");
                var config = new AmazonS3Config
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName("us-east-2") // CHANGE REGION TO YOURS
                };
                using var client = new AmazonS3Client(credentials, config);

                // INITIALIZE THE S3 TRANSFERUTILITY OBJECT WHICH DOES UPLOAD & DOWNLOAD
                TransferUtility fileTransferUtility = new TransferUtility(client);

                // https://learn.microsoft.com/en-us/dotnet/api/system.io.path.gettemppath?view=net-7.0&tabs=windows#remarks
                string pathway = Path.GetTempPath();

                // DEFINE S3 DOWNLOAD REQUEST
                var downloadRequest = new TransferUtilityDownloadRequest()
                {
                    FilePath = pathway + upload,
                    BucketName = "gcpdf",
                    Key = upload

                };

                // DOWNLOAD THE PDF ARTIFACT ASYNCHRONOUSLY, WAIT TILL COMPLETION
                await fileTransferUtility.DownloadAsync(downloadRequest);

                // CREATE SOURCE PDF OBJECT
                GcPdfDocument pdf = new();
                var fs = new FileStream(Path.Combine(pathway + upload), FileMode.Open, FileAccess.Read);

                // LOAD SOURCE i.e S3 "UPLOAD" PDF
                pdf.Load(fs);

                // SAVE IN TEMPORARY PATH AS invoiceBerlinPDF-convert.svg  
                pdf.Pages[0].SaveAsSvg(pathway + "invoiceBerlinPDF-convert.svg", null,
                         new SaveAsImageOptions() { Zoom = 2f },
                         new XmlWriterSettings() { Indent = true });
                string path = pathway + "invoiceBerlinPDF-convert.svg";


                // UPLOAD SVG TO S3 BUCKET
                var stream = new MemoryStream(System.IO.File.ReadAllBytes(path))
                {
                    Position = 0
                };

                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = stream,
                    Key = "invoiceBerlinPDF-convert.svg",
                    BucketName = "gcpdf",
                    CannedACL = S3CannedACL.PublicRead
                };

                await fileTransferUtility.UploadAsync(uploadRequest);


                // RETURN DOWNLOAD S3 URL OF SVG
                GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
                {
                    BucketName = "gcpdf",
                    Key = "invoiceBerlinPDF-convert.svg",
                    Expires = DateTime.Now.AddMinutes(5)
                };

                string outputt = "filelink: " + client.GetPreSignedURL(request);
                return new string[] { "Pdf converted to SVG format", outputt };
            }

            return new string[] { "StatusCode: " + HttpStatusCode.BadRequest, "Is Key2: \"pdf2svg\"? " };
        }
    }


}