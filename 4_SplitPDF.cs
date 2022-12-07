using Microsoft.AspNetCore.Mvc;
using GrapeCity.Documents.Pdf;
using Amazon.S3.Transfer;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.IO.Compression;
using GrapeCity.Documents.Common;
using System.Text;

namespace GrapeCityDocsAWSLambda.Controllers;

// SplitPdfs CLASS DEFINES INPUT PARAMETERS - KEY & VALUE
public class SplitPdfs
{
    public string? Key1 { get; set; }
    public int Key2 { get; set; }
}


[Route("api/[controller]")]
public class ValuesController : ControllerBase
{

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
                    pdf1.Save(pathway+"page1.pdf");
                }

                if (i == 1)
                {
                    pdf2.MergeWithDocument(pdf, new MergeDocumentOptions()
                    {
                        PagesRange = pageRange // (2,2)
                    });
                    pdf2.Save(pathway+"page2.pdf");
                }


                if (i == 2)
                {
                    pdf3.MergeWithDocument(pdf, new MergeDocumentOptions()
                    {
                        PagesRange = pageRange // (3,3)
                    });
                    pdf3.Save(pathway+"page3.pdf");
                }

            }

            // FULLPATH TO ZIP
            string path = pathway+"SplitPDFByPage.zip";

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
}
