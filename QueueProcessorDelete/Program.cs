using System;
using System.Text.Json;
using System.Threading;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;


Console.WriteLine("Hello to the QueueProcessor!");            

var queueClient = new QueueClient(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING"), "pics-to-delete");

queueClient.CreateIfNotExists();

while (true)
{
   QueueMessage message = queueClient.ReceiveMessage();

    if (message != null)
                {
                    Console.WriteLine($"Message received {message.Body}");

                    var task = JsonSerializer.Deserialize<Task>(message.Body);
                    Console.WriteLine(task.heroeName);
                    Console.WriteLine(task.alterEgoName);
                    //Console.WriteLine($"Let's rename {task.oldName} to {task.newName}");

                    if (task.heroeName != null)
                    {
                        //Create a Blob service client
                        var blobClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING"));

                        //Get container client
                        BlobContainerClient containerHeroes = blobClient.GetBlobContainerClient("heroes");
                        BlobContainerClient containerAlteregos = blobClient.GetBlobContainerClient("alteregos");

                        //Get blob with name
                        var heroeFileName = $"{task.heroeName.Replace(' ', '-').ToLower()}.jpeg";
                        var alterEgoFileName = $"{task.alterEgoName.Replace(' ', '-').ToLower()}.png";
                        Console.WriteLine($"Looking for {heroeFileName}");
                        var heroeBlob = containerHeroes.GetBlobClient(heroeFileName);    
                        Console.WriteLine($"Looking for {alterEgoFileName}");
                        var alterEgoBlob = containerAlteregos.GetBlobClient(alterEgoFileName);                     

                        if (heroeBlob.Exists())
                        {
                            Console.WriteLine("Found it!");
                            /*
                            var newFileName = $"{task.newName.Replace(' ', '-').ToLower()}.png";
                            Console.WriteLine($"Renaming {oldFileName} to {newFileName}");

                            //Create a new blob with the new name                            
                            BlobClient newBlob = container.GetBlobClient(newFileName);

                            //Copy the content of the old blob into the new blob
                            newBlob.StartCopyFromUri(oldBlob.Uri);
                            */
                            //Delete the old blob
                            heroeBlob.DeleteIfExists();

                            //Delete message from the queue
                            queueClient.DeleteMessage(message.MessageId,message.PopReceipt);
                            if (alterEgoBlob.Exists()){
                                Console.WriteLine("Found it!");
                                alterEgoBlob.DeleteIfExists();
                            }
                            else
                            {
                                Console.WriteLine($"There is no alterEgo image to delete.");
                                Console.WriteLine($"Dismiss task.");
                                //Delete message from the queue
                                queueClient.DeleteMessage(message.MessageId, message.PopReceipt);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"There is no heroe image to delete.");
                            Console.WriteLine($"Dismiss task.");
                            //Delete message from the queue
                            queueClient.DeleteMessage(message.MessageId, message.PopReceipt);
                        }

                    }
                    else
                    {
                        Console.WriteLine($"Bad message. Delete it");
                        //Delete message from the queue
                        queueClient.DeleteMessage(message.MessageId, message.PopReceipt);
                        
                    }
                }
                else
                {
                    Console.WriteLine($"Let's wait 5 seconds");
                    Thread.Sleep(5000);
                }

            }

class Task
{
    public string heroeName { get; set; }
    public string alterEgoName { get; set; }
}
