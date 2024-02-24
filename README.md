# Procesos en segundo plano en Microsoft Azure

Ahora que ya has desplegado tu primera aplicación y que sabes cómo almacenar las imágenes de tus héroes en Azure Storage, vamos a ver cómo ejecutar procesos en segundo plano que trabajen con estos assets.

Pero antes de nada, **¿qué es un proceso en segundo plano?** Un proceso en segundo plano es un programa que se ejecuta en el sistema operativo sin interacción con el usuario. En el caso de una aplicación web, un proceso en segundo plano puede ser útil para realizar tareas que no necesitan ser ejecutadas en el contexto de una petición HTTP, como por ejemplo, enviar correos electrónicos, procesar imágenes, o realizar tareas de mantenimiento.

Para que puedas probar cómo ejecutar estos procesos en segundo plano en diferentes servicios, y que no te suponga un coste adicional, vamos a utilizar **Azurite**. [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage) es un emulador de Azure Storage que puedes ejecutar en tu máquina local. De esta forma, no necesitarás una cuenta de Azure para probar los ejemplos de este capítulo 🥳.

Puedes utilizar tanto la extensión de [Azurite para Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=Azurite.azurite), como la imagen de Docker, ahora que ya aprendiste Docker... ¡en 3 horas 😅!


```bash
docker run \
--name azurite \
-d \
-p 10000:10000 \
-p 10001:10001 \
mcr.microsoft.com/azure-storage/azurite
```
Para subir a este emulador las imágenes, de los héroes y de los alter egos, puedes utilizar el mismo comando que vimos en la sesión anterior:

Para los heroes:

```bash
az storage blob upload-batch \
--destination heroes \
--source assets/heroes/. \
--connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
```

Para los alter egos:

```bash
az storage blob upload-batch \
--destination alteregos \
--source assets/alteregos/png/. \
--connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
```

Para esta temática, se ha actualizado tanto el proyecto de Angular como la API en .NET para que seamos capaces de subir imágenes a Azure Storage. Puedes encontrar el código en la carpeta `front-end` y `back-end` respectivamente. ¡Vamos a probarlo!

Antes de nada asegurate que el contenedor de SQL server que creamos en el primer capítulo está en ejecución. Si no es así, puedes ejecutarlo con el siguiente comando:

Para que esta API pueda funcionar necesitarás además de una base de datos SQL Server. Como venimos de una clase de Docker en tres horas 😙 y sabemos que podemos utilizar un contenedor para hospedar la misma vamos a crearlo con el siguiente comando:

Crear:
```bash
docker run \
--name sqlserver \
-e 'ACCEPT_EULA=1' \
-e 'MSSQL_SA_PASSWORD=Password1!' \
 -v mssqlserver_volume:/var/opt/mssql \
-p 1433:1433 \
-d mcr.microsoft.com/azure-sql-edge
```

O Arrancar:
```bash
docker start sqlserver
```

Ejecuta la API utilizando estos comandos:

```bash
cd back-end
dotnet run
```

En esta versión nuestra API ya viene configurada para trabajar con Azurite. Lo único que se ha tenido que modificar es la cadena de conexión a **UseDevelopmentStorage=true**.

```json{5}
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=localhost,1433;Initial Catalog=heroes;Persist Security Info=False;User ID=sa;Password=Password1!;Encrypt=False",
        "AzureStorage": "UseDevelopmentStorage=true"
    },
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning",
            "Microsoft.Hosting.Lifetime": "Information"
        }
    },
    "AllowedHosts": "*"
}
```
Con ello el SDK ya sabe que tiene que comunicarse con Azurite en lugar de con Azure Storage 🥳.

Por otro lado, se ha añadido un método adicional llamado `GetAlterEgoPicSas`. Este nos permitirá recuperar una clave temporal que le permita a nuestra app en angular subir imágenes para los alter egos.

```csharp
        // GET: api/hero/alteregopic/sas
        [HttpGet("alteregopic/sas/{imgName}")]
        public ActionResult GetAlterEgoPicSas(string imgName)
        {
            //Get image from Azure Storage
            string connectionString = _configuration.GetConnectionString("AzureStorage");

            // Create a BlobServiceClient object which will be used to create a container client
            var blobServiceClient = new BlobServiceClient(connectionString);

            //Get container client
            var containerClient = blobServiceClient.GetBlobContainerClient("alteregos");

            //Get blob client
            var blobClient = containerClient.GetBlobClient(imgName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "alteregos",
                BlobName = imgName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(3)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write);

            Uri sasUri = blobClient.GenerateSasUri(sasBuilder);

            Console.WriteLine($"SAS Uri for blob is: {sasUri}");

            //return image
            return Ok($"{blobServiceClient.Uri}{sasUri.Query}");
        }    
    }
```

Ejecuta el front-end utilizando estos comandos:

```bash
cd 01-stack-relacional/03-cloud/azure/03-procesos-en-segundo-plano/front-end
npm install
npm start
```

Si intentas subir una imágen de un alter ego te dará error.

<img src="images/Error de CORS de Azurite.png" />

Esto es porque aunque tengas una clave temporal es necesario que la cuenta de almacenamiento tenga configurado CORS. Para ello puedes ejecutar el siguiente comando:

```bash
az storage cors add \
--methods GET POST PUT DELETE \
--origins '*' \
--services b \
--max-age 60 \
--allowed-headers '*' \
--exposed-headers '*' \
--connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"
```
Ahora intenta de nuevo a modificar la imágen 🙃

¡Genial ahora que ya tienes la nueva versión de Tour Of Heroes funcionando con Azurite, vamos a empezar a investigar formas de ejecutar procesos en segundo plano! 🚀

# Procesos en segundo plano con Azure Functions

## ¿Qué es Azure Functions?

Azure Functions es un servicio de cómputo sin servidor que te permite ejecutar código en respuesta a eventos sin tener que preocuparte por la infraestructura. Puedes utilizar Azure Functions para ejecutar un script o pieza de código en respuesta a una variedad de eventos. Estos eventos pueden ser desencadenados por cambios en datos, la ejecución de un cronograma, interacciones con un servicio HTTP, o la recepción de mensajes en una cola, entre otros.

En este ejemplo vamos a hacer una Azure Function, también en local, que nos van a permitir arreglar un problema que ahora mismo tiene mi aplicación: y es que si cambio la imagen de un alter ego en formato jpeg, la aplicación no la va a poder mostrar, ya que solo entiende de imágenes en formato png 🤔. Por lo que vamos a crear un método para este servicio que si detecta que subimos un jpeg lo convierta a png para que mi aplicación siga funcionando.

## Crear una Azure Function

Para crear una Azure Function en local, necesitamos instalar el Azure Functions Core Tools. Puedes hacerlo con el siguiente comando:

```bash
npm i -g azure-functions-core-tools@4 --unsafe-perm true
```

Una vez instalado, vamos a crear un nuevo proyecto de Azure Functions. Para ello, ejecuta el siguiente comando:

```bash
mkdir -p azure-functions
cd azure-functions
func init
```
El último comando iniciará un asistente donde tenemos que elegir el lenguaje de programación con el que queremos desarrollar esta Azure Function. Para este ejemplo elegiremos `1.dotnet` y en el segundo paso el lenguaje será `1.c#`. Una vez finalice la creación la misma estará disponible en la carpeta `01-stack-relacional/03-cloud/azure/03-procesos-en-segundo-plano/azure-functions`.

```bash
cd 01-stack-relacional/03-cloud/azure/03-procesos-en-segundo-plano/azure-functions
```

¡Pero esto es solo el proyecto! ahora lo que hace falta es crear las funciones como tal. Para este ejemplo vamos a crear una que escuche nuestra cuenta de almacenamiento, en este caso Azurite, y que si detecta en el contenedor alter egos que se sube una imagen en formato jpeg, la convierta a png.

Para ello, ejecuta el siguiente comando:

```bash
func new
```
Ahora en el asistente elige la opción `3. BlobTrigger` y dale un nombre a la función, por ejemplo `ConvertImageToPng`. En el siguiente paso elige el lenguaje de programación, en este caso `1. C#`. Por último, elige el nombre del contenedor que quieres que escuche, en este caso `alteregos`. Una vez generada verás que dentro del directorio `azure-functions` tenemos un nueva clase llamada `ConvertImageToPng.cs`.

Reemplaza el contenido por el siguiente:

```csharp
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace azure_functions
{
    public class ConvertImageToPng
    {
        [FunctionName("ConvertImageToPng")]
        public void Run([BlobTrigger("alteregos/{name}.jpeg", Connection = "AzureStorageConnection")] Stream myBlob, string name, ILogger log,
            [Blob("alteregos/{name}.png", FileAccess.Write, Connection = "AzureStorageConnection")] Stream outputBlob)
        {            
            log.LogInformation($"Converting {name}.jpeg to {name}.png");
            
            using (var image = Image.Load(myBlob))
            {
                image.SaveAsPng(outputBlob);
            }          
        }
    }
}
```

Como ves, la función es muy sencilla: escucha el contenedor `alteregos` y si detecta que se sube una imagen en formato jpeg, la convierte a png. Para ello utiliza la librería `SixLabors.ImageSharp` que es una librería de manipulación de imágenes de alto rendimiento y fácil de usar para .NET.

Para añadir la referencia a esta librería, ejecuta el siguiente comando:

```bash
dotnet add package SixLabors.ImageSharp
```

y añadir la configuración en `local.settings.json` para que pueda conectarse con Azurite:

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "AzureStorageConnection": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet"
    }
}
```

Ahora que ya tenemos la función, vamos a ejecutarla. Para ello, ejecuta el siguiente comando:

```bash
func start
```

Si todo ha ha ido bien deberías de ver algo como lo siguiente en el terminal:

<img src="images/Azure Function con BlobTrigger ejecutandose.png" />

Ahora la prueba de fuego 🔥 sería subir una imagen al contenedor de alter egos y comprobar que efectivamente esta función se ejecuta y que tenemos en el propio contenedor el resultado guardado en png. Para ello, elimina todas las imagenes que hay en el contenedor con Azure Storage Explorer y utiliza las imágenes guardadas en `assets/alteregos/jpeg`.

También puedes probar desde la interfaz en Angular.

## Azure Logic Apps

Otro servicio que puede ayudarnos con los procesos en segudo plano se llama Azure Logic Apps, el cual de una forma gráfica nos permite generar flujos de trabajo que se ejecutan en respuesta a eventos. Para poder verlo con otro ejemplo vamos a crear uno que se ejecute cada vez que subimos una nueva imagen de un héroes al contenedor `heroes` y que la convierta en algo un poquito más artístico para poder mostrarla en el apartado de Dashboard. Crea la Azure Logic App a través del portal de Azure y genera un flujo como el siguiente:

<img src="images/Flujo de Azure Logic Apps contraido.png" />

Y este es el mismo flujo con todos los detalles:

<img src="images/Flujo de Azure Logic Apps con todos los detalles.png" />

Esta utiliza un conector que necesita de una API Key para poder utilizarlo, para ello debes ir a la web de [cloudmersive y aplicar para el plan gratuito](https://cloudmersive.com/pricing-small-business).

El resultado será como el siguiente:

## Azure App Service WebJobs

Por último, existe un servicio como parte de App Service que se llama WebJobs el cual nos permite también aprovechar el computo de nuestro plan para procesos en segundo plano.

Otro de los problemas que tiene nuestra aplicación es que cada vez que renombramos un alter ego, no se actualiza el nombre de la imagen en el contenedor de Azure Storage. Para ello vamos a modificar la API para que en la operación PUT introduzca un mensaje en una cola de Azure Storage de la siguiente forma:

```csharp
        // PUT: api/Hero/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutHero(int id, Hero hero)
        {
            if (id != hero.Id)
            {
                return BadRequest();
            }
            var oldHero = await _context.Heroes.FindAsync(id);
            _context.Entry(oldHero).State = EntityState.Detached;


            _context.Entry(hero).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                /*********** Background processs (We have to rename the image) *************/
                if (hero.AlterEgo != oldHero.AlterEgo)
                {
                    // Get the connection string from app settings
                    string connectionString = _configuration.GetConnectionString("AzureStorage");

                    // Instantiate a QueueClient which will be used to create and manipulate the queue
                    var queueClient = new QueueClient(connectionString, "alteregos");

                    // Create a queue
                    await queueClient.CreateIfNotExistsAsync();

                    // Create a dynamic object to hold the message
                    var message = new
                    {
                        oldName = oldHero.AlterEgo,
                        newName = hero.AlterEgo
                    };

                    // Send the message
                    await queueClient.SendMessageAsync(JsonSerializer.Serialize(message).ToString());

                }
                /*********** End Background processs *************/
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HeroExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }
```

Por otro lado, tenemos una aplicación de consola que se encarga de escuchar esta cola de mensajes y que se encarga de renombrar la imagen en el contenedor de Azure Storage. 

Para ello crea una nueva aplicación de consola con el siguiente comando:

```bash
cd ..
mkdir QueueProcessorDelete
cd QueueProcessorDelete
dotnet new console 
```

El código de esta aplicación es el siguiente:

```csharp
using System;
using System.Text.Json;
using System.Threading;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;


Console.WriteLine("Hello to the QueueProcessor!");            

var queueClient = new QueueClient(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING"), "alteregos");

queueClient.CreateIfNotExists();

while (true)
{
   QueueMessage message = queueClient.ReceiveMessage();

    if (message != null)
                {
                    Console.WriteLine($"Message received {message.Body}");

                    var task = JsonSerializer.Deserialize<Task>(message.Body);

                    Console.WriteLine($"Let's rename {task.oldName} to {task.newName}");

                    if (task.oldName != null)
                    {
                        //Create a Blob service client
                        var blobClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING"));

                        //Get container client
                        BlobContainerClient container = blobClient.GetBlobContainerClient("alteregos");

                        //Get blob with old name
                        var oldFileName = $"{task.oldName.Replace(' ', '-').ToLower()}.png";
                        Console.WriteLine($"Looking for {oldFileName}");
                        var oldBlob = container.GetBlobClient(oldFileName);                        

                        if (oldBlob.Exists())
                        {
                            Console.WriteLine("Found it!");
                            var newFileName = $"{task.newName.Replace(' ', '-').ToLower()}.png";
                            Console.WriteLine($"Renaming {oldFileName} to {newFileName}");

                            //Create a new blob with the new name                            
                            BlobClient newBlob = container.GetBlobClient(newFileName);

                            //Copy the content of the old blob into the new blob
                            newBlob.StartCopyFromUri(oldBlob.Uri);

                            //Delete the old blob
                            oldBlob.DeleteIfExists();

                            //Delete message from the queue
                            queueClient.DeleteMessage(message.MessageId,message.PopReceipt);
                        }
                        else
                        {
                            Console.WriteLine($"There is no old image to rename.");
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
    public string oldName { get; set; }
    public string newName { get; set; }
}
```

Este lo que hace es que se queda escuchando una cola llamada alteregos que cuando recibe un mensaje, lo procesa y realiza una acción. En este caso, recibe un mensaje con el nombre de la imagen que queremos renombrar y el nuevo nombre que queremos que tenga. Si la imagen existe, la renombra y si no, no hace nada.

```bash
AZURE_STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true" dotnet run
```

## Laboratorio Módulo 4 - Azure
### Ejercicio 1 (Obligatorio)
El objetivo de este ejercicio es modificar el método DeleteHero de la API, que usa nuestra aplicación Tour Of Heroes, para que cada vez que se elimine un héroe se escriba un mensaje en una cola de Azure Storage que diga de qué héroe se debe eliminar su imagen, tanto del héroe como de su alter ego, ya que de no hacerlo el héroe se eliminará de la base de datos, pero no las imágenes que se asociaron a este (y queda guarrería). Además, una vez que se borre el héroe ya no sabremos qué imágenes habría que borrar, así que es el momento 😊 el nombre de la cola debe ser pics-to-delete.

#### App Service WebJobs

Hemos creado un método Delete en el "HeroController.cs" del "BackEnd" el cual crea un webjobs que añadira una cola en "Queues" llamada "pics-to-delete" en Azure Storage (En nuestro caso Azurite Doquerizado)
<img src="images/azurite.jpg" />
De tal modo que cuando se borre un Heroes se creará una tarea en la cola "pics-to-delete"
<img src="images/azureStorage.jpg" />

Donde el metodo Put es el siguiente:
```csharp
        // DELETE: api/Hero/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHero(int id)
        {         
           
            try{
                 
                  var hero = _heroRepository.GetById(id);
                 
                // Get the connection string from app settings
                string connectionString = _configuration.GetConnectionString("AzureStorage");

                // Instantiate a QueueClient which will be used to create and manipulate the queue
                var queueClient = new QueueClient(connectionString, "pics-to-delete");
                
                // Create a queue
                await queueClient.CreateIfNotExistsAsync();
                // Create a dynamic object to hold the message
                var message = new
                {
                    oldName = hero.Name,
                    newName = "none Heroe"
                };

                // Send the message
                await queueClient.SendMessageAsync(JsonSerializer.Serialize(message).ToString());
                _heroRepository.Delete(id);
            }
            catch (Exception ex)
            {

                return NotFound(ex.Message);              
                
            }

            return NoContent();
        }
```
Para arrancar de nuevo el BAckEnd
```
$ dotnet run
```
Podremos ver un mensaje como el siguiente al morrar un Heroe
<img src="images/azureStorage.jpg" />

### Ejercicio 2 (Obligatorio)
Ahora que ya tienes mensajes que procesar, necesitas algo que lo procese 😊 para ello tienes que crear un proceso en segundo plano que te ayude en esta tarea. Para esto vamos a crear una aplicación de Consola (que potencialmente se despligue en un WebJob). Esta deberá:

Recuperar los mensajes de la cola pics-to-delete.
Buscar los blobs que almacenan la imagen del héroe
Eliminar los blobs encontrados.

Para ellos hemos creado una aplicacion de consola llamada "QueueProcessorDelete"
```csharp
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

                    //Console.WriteLine($"Let's rename {task.oldName} to {task.newName}");

                    if (task.oldName != null)
                    {
                        //Create a Blob service client
                        var blobClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING"));

                        //Get container client
                        BlobContainerClient containerHeroes = blobClient.GetBlobContainerClient("heroes");
                        BlobContainerClient containerAlteregos = blobClient.GetBlobContainerClient("alteregos");

                        //Get blob with old name
                        //var oldFileName = $"{task.oldName.Replace(' ', '-').ToLower()}.png";
                        var oldFileName = $"{task.oldName.Replace(' ', '-').ToLower()}.jpeg";
                        Console.WriteLine($"Looking for {oldFileName}");
                        var oldBlob = containerHeroes.GetBlobClient(oldFileName);                        

                        if (oldBlob.Exists())
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
                            oldBlob.DeleteIfExists();

                            //Delete message from the queue
                            queueClient.DeleteMessage(message.MessageId,message.PopReceipt);
                        }
                        else
                        {
                            Console.WriteLine($"There is no old image to rename.");
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
    public string oldName { get; set; }
    public string newName { get; set; }
}

```

Donde para arrancarla debemos:
```
 AZURE_STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true" dotnet run
```

Apareciendo en consola que ha borrado la imagen encontrada que indicaba la tarea pendiente

<img src="images/consola.jpg" />

Si nos fijamos podremos ver que la imagen de spiderman ha desaparecido:

<img src="images/spidermaDeleted.jpg" />


### Reiniciar Toda la Arquitectura
Ejecuta la API utilizando estos comandos:

```bash
cd /03-procesos-en-segundo-plano/back-end
dotnet run
```
Ejecuta el front-end utilizando estos comandos:

```bash
cd /03-procesos-en-segundo-plano/front-end
npm start
```
Ejecutar la aplicacion de consola para convertir los alterego.jpeg a png:
```bash
cd /03-procesos-en-segundo-plano/QueueProcessor
 AZURE_STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true" dotnet run
```
Ejecutar la aplicacion de consola para borrar imagen de Heroe:
```bash
cd /03-procesos-en-segundo-plano/QueueProcessorDelete
 AZURE_STORAGE_CONNECTION_STRING="UseDevelopmentStorage=true" dotnet run
```
Meter un heroe borrado
Click en el heroe contenido en:
> /03-procesos-en-segundo-plano/back-end/client.http