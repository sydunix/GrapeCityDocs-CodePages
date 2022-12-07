using Microsoft.AspNetCore.Mvc;
using GrapeCity.Documents.Pdf;
using Amazon.S3.Transfer;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using GrapeCity.Documents.Pdf.Annotations;
using System.Drawing;
using GrapeCity.Documents.Drawing;


public class WatermarkPdf
{
    public string? Key1 { get; set; }
    public string? Key2 { get; set; }
}

[Route("api/[controller]")]
public class ValuesController : ControllerBase
{
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
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName("us-east-2") // CHANGE REGION TO YOURS
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

}