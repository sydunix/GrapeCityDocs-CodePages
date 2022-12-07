using Microsoft.AspNetCore.Mvc;
using GrapeCity.Documents.Pdf;
using Amazon.S3.Transfer;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text;
using System.Net;
using System.Xml;


namespace GrapeCityDocsAWSLambda.Controllers;

public class Convert
{
    public string? Key1 { get; set; }
    public string? Key2 { get; set; }
}

[Route("api/[controller]")]
public class ValuesController : ControllerBase
{
    

    // GET api/values/convert/format
    [HttpGet("convert/format")]
    public async Task<IEnumerable<string>> GetAsync(int id, [FromQuery]Convert key)
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