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

    // On Postman App, go to body, form-data
    // Type files in Key field
    // Also in Key field, select file in the dropdown menu (instead of text)

    // Next, go to Value field, highlight the four files where located, all at once

    // Then Select POST method

    // Click send
    
    [HttpPost("upload")]
    public async Task<IActionResult> OnPostUploadAsync([FromForm] List<IFormFile> files)
    {


        var credentials = new BasicAWSCredentials("MYACCESSKEY", "MYSECRETKEY");
        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName("REGION") //Example us-east-2
        };
        using var client = new AmazonS3Client(credentials, config);

        long size = files.Sum(f => f.Length);
        Console.WriteLine("Hey: " + size);



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

        // Process uploaded files
        // Don't rely on or trust the FileName property without validation.

        return Ok(new { count = files.Count, size });
    }

}