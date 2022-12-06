using Microsoft.AspNetCore.Mvc;
using GrapeCity.Documents.Pdf;
using Amazon.S3.Transfer;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace GrapeCityDocsAWSLambda.Controllers;


// MergePdfs CLASS DEFINES INPUT PARAMETES - KEY & VALUE
public class MergePdfs
{
    public string? Key1 { get; set; }
    public string? Key2 { get; set; }
}

[Route("api/[controller]")]
public class ValuesController : ControllerBase
{

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


            // RETURN DOWNLOAD S3 URL OF MERGED PDF
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
}
