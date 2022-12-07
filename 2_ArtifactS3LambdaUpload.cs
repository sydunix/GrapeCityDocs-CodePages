using Microsoft.AspNetCore.Mvc;
using GrapeCity.Documents.Pdf;
using Amazon.S3.Transfer;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace GrapeCityDocsAWSLambda.Controllers;

[Route("api/[controller]")]
public class ValuesController : ControllerBase
{


    // POST api/values/upload

    // ON POSTMAN APP, GO TO "Body", "form-data"
    // TYPE "files" IN key FIELD
    // IN key FIELD also, SELECT File IN THE DROPDOWN MENU (INSTEAD OF TEXT)
    // NEXT, GO TO value FIELD, SELECT ALL FOUR(4) files YOU WISH TO UPLOAD TO S3
    // NOTE THE DEFAULT NAME GIVEN TO S3 BUCKET -> "gcpdf" YOU CAN CHANGE THIS IN CODE
    // NOTE THAT YOU WILL NEED YOUR ACCESSKEY AND SECRETKEY ALREADY MAPPED TO APPROPRIATE IAM ROLE
    // THEN SELECT POST METHOD AND CLICK SEND
    
    [HttpPost("upload")]
    public async Task<IActionResult> OnPostUploadAsync([FromForm] List<IFormFile> files)
    {
       
        // INITIALIZE AWS S3 SDK CREDENTIALS, REGION, CLIENT
        var credentials = new BasicAWSCredentials("MYACCESSKEY", "MYSECRETKEY");
        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName("region") // WHERE example of region = us-east-2
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
        // DO NOT RELY OR TRUST THE FileName PROPERTY WITHOUT VALIDATION

        return Ok(new { count = files.Count, size });
    }

}
