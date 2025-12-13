# Nethereum.Util.Rest

Generic REST client abstraction and implementation for HTTP API interactions with JSON serialization support.

## Overview

Nethereum.Util.Rest provides a lightweight wrapper around HttpClient for making REST API calls with automatic JSON serialization/deserialization. It's designed to simplify HTTP communication in Nethereum components and user applications.

**Key Features:**
- Generic interface for RESTful HTTP operations (GET, POST, PUT, DELETE)
- Automatic JSON serialization/deserialization using System.Text.Json (.NET 8+) or Newtonsoft.Json (older versions)
- Support for custom headers including Bearer token authentication
- Multipart form data support for file uploads
- HttpClient extension methods with built-in auth support
- Dependency injection friendly with IRestHttpHelper interface
- Used internally by Nethereum.Beaconchain, Nethereum.DataServices, and other packages

## Installation

```bash
dotnet add package Nethereum.Util.Rest
```

Or via Package Manager Console:

```powershell
Install-Package Nethereum.Util.Rest
```

## Dependencies

**External:**
- System.Text.Json (.NET 8+) or Newtonsoft.Json (older frameworks)
- System.Net.Http.HttpClient

**Nethereum:**
- None (this is a foundational utility package)

## Key Concepts

### IRestHttpHelper Interface

The `IRestHttpHelper` interface provides a generic abstraction for REST operations:

```csharp
public interface IRestHttpHelper
{
    Task<T> GetAsync<T>(string path, Dictionary<string, string> headers = null);
    Task<TResponse> PostAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null);
    Task<TResponse> PutAsync<TResponse, TRequest>(string path, TRequest request, Dictionary<string, string> headers = null);
    Task DeleteAsync(string path, Dictionary<string, string> headers = null);
    Task<TResponse> PostMultipartAsync<TResponse>(string path, MultipartFormDataRequest request, Dictionary<string, string> headers = null);
}
```

### RestHttpHelper Implementation

Concrete implementation that wraps HttpClient and handles JSON serialization automatically.

### HttpClient Extensions

Extension methods for HttpClient with built-in Bearer token authentication support (NET 5.0+).

### JSON Serialization Strategy

- **.NET 8+**: Uses `System.Text.Json` for better performance
- **Older versions**: Uses `Newtonsoft.Json` for compatibility
- Transparent to API consumers - same interface across all frameworks

## Quick Start

```csharp
using Nethereum.Util.Rest;
using System.Net.Http;

// Create REST helper
var httpClient = new HttpClient();
var restHelper = new RestHttpHelper(httpClient);

// Make a GET request
var data = await restHelper.GetAsync<MyDataModel>("https://api.example.com/data");

// Make a POST request
var response = await restHelper.PostAsync<ResponseModel, RequestModel>(
    "https://api.example.com/submit",
    new RequestModel { Value = "test" }
);
```

## Usage Examples

### Example 1: Basic GET Request with Custom Headers

```csharp
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Net.Http;

// Create REST helper
var restHelper = new RestHttpHelper(new HttpClient());

// Define custom headers
var headers = new Dictionary<string, string>
{
    { "accept", "application/json" }
};

// Make GET request
var result = await restHelper.GetAsync<ApiResponse>(
    "https://api.example.com/v1/data",
    headers
);

public class ApiResponse
{
    public string Status { get; set; }
    public object Data { get; set; }
}
```

### Example 2: Dependency Injection Pattern (Real Example from BeaconApiClient)

```csharp
using Nethereum.Util.Rest;
using System.Net.Http;

// Service class using dependency injection
public class BeaconApiClient
{
    private readonly IRestHttpHelper _restHelper;
    public string BaseUrl { get; }

    // Constructor with HttpClient
    public BeaconApiClient(string baseUrl, HttpClient httpClient = null)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _restHelper = new RestHttpHelper(httpClient ?? new HttpClient());
    }

    // Constructor with IRestHttpHelper (for testing)
    public BeaconApiClient(string baseUrl, IRestHttpHelper restHelper)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _restHelper = restHelper;
    }

    // Example method
    public async Task<BootstrapResponse> GetBootstrapAsync(string blockRoot)
    {
        var url = $"{BaseUrl}/eth/v1/beacon/light_client/bootstrap/{blockRoot}";
        return await _restHelper.GetAsync<BootstrapResponse>(url);
    }
}
```

### Example 3: 4byte.directory API Integration (Real Example)

```csharp
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

public class FourByteDirectoryService
{
    public const string BaseUrl = "https://www.4byte.directory";
    private IRestHttpHelper _restHttpHelper;

    public FourByteDirectoryService()
    {
        _restHttpHelper = new RestHttpHelper(new HttpClient());
    }

    public FourByteDirectoryService(IRestHttpHelper restHttpHelper)
    {
        _restHttpHelper = restHttpHelper;
    }

    // Get function signature by hex signature (e.g., "0xa9059cbb")
    public Task<FourByteDirectoryResponse> GetFunctionSignatureByHexSignatureAsync(
        string hexSignature)
    {
        var url = $"{BaseUrl}/api/v1/signatures/?hex_signature={hexSignature}";
        return GetDataAsync<FourByteDirectoryResponse>(url);
    }

    // Get function signature by text (e.g., "transfer(address,uint256)")
    public Task<FourByteDirectoryResponse> GetFunctionSignatureByTextSignatureAsync(
        string textSignature)
    {
        var url = $"{BaseUrl}/api/v1/signatures/?text_signature={textSignature}";
        return GetDataAsync<FourByteDirectoryResponse>(url);
    }

    private async Task<T> GetDataAsync<T>(string url)
    {
        var headers = new Dictionary<string, string>
        {
            { "accept", "application/json" }
        };

        return await _restHttpHelper.GetAsync<T>(url, headers);
    }
}
```

### Example 4: Sourcify Contract Verification API (Real Example)

```csharp
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

public class SourcifyApiService
{
    public const string BaseUrl = "https://sourcify.dev/server/";
    public const string BaseUrlMeta = "https://repo.sourcify.dev/";
    private IRestHttpHelper restHttpHelper;

    public SourcifyApiService()
    {
        restHttpHelper = new RestHttpHelper(new HttpClient());
    }

    public SourcifyApiService(HttpClient httpClient)
    {
        restHttpHelper = new RestHttpHelper(httpClient);
    }

    // Get compilation metadata for verified contract
    public Task<CompilationMetadata> GetCompilationMetadataAsync(
        long chain,
        string address,
        bool fullMatch = true)
    {
        var matchType = fullMatch ? "full_match" : "partial_match";
        var url = $"{BaseUrlMeta}/contracts/{matchType}/{chain}/{address}/metadata.json";
        return GetDataAsync<CompilationMetadata>(url);
    }

    // Get source files for verified contract
    public Task<List<SourcifyContentFile>> GetSourceFilesFullMatchAsync(
        long chain,
        string address)
    {
        var url = $"{BaseUrl}/files/{chain}/{address}";
        return GetDataAsync<List<SourcifyContentFile>>(url);
    }

    private Task<T> GetDataAsync<T>(string url)
    {
        var headers = new Dictionary<string, string>
        {
            { "accept", "application/json" }
        };

        return restHttpHelper.GetAsync<T>(url, headers);
    }
}
```

### Example 5: POST Request with Custom Object

```csharp
using Nethereum.Util.Rest;
using System.Net.Http;

var restHelper = new RestHttpHelper(new HttpClient());

// Define request and response models
public class CreateUserRequest
{
    public string Username { get; set; }
    public string Email { get; set; }
}

public class CreateUserResponse
{
    public int UserId { get; set; }
    public string Status { get; set; }
}

// Make POST request
var request = new CreateUserRequest
{
    Username = "alice",
    Email = "alice@example.com"
};

var response = await restHelper.PostAsync<CreateUserResponse, CreateUserRequest>(
    "https://api.example.com/users",
    request
);

Console.WriteLine($"Created user ID: {response.UserId}");
```

### Example 6: PUT Request for Updates

```csharp
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Net.Http;

var restHelper = new RestHttpHelper(new HttpClient());

public class UpdateProfileRequest
{
    public string DisplayName { get; set; }
    public string Bio { get; set; }
}

public class UpdateProfileResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
}

// Update with custom headers
var headers = new Dictionary<string, string>
{
    { "X-Api-Version", "2.0" }
};

var updateRequest = new UpdateProfileRequest
{
    DisplayName = "Alice Smith",
    Bio = "Blockchain developer"
};

var result = await restHelper.PutAsync<UpdateProfileResponse, UpdateProfileRequest>(
    "https://api.example.com/profile/123",
    updateRequest,
    headers
);
```

### Example 7: DELETE Request

```csharp
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Net.Http;

var restHelper = new RestHttpHelper(new HttpClient());

// Delete with authentication header
var headers = new Dictionary<string, string>
{
    { "Authorization", "Bearer YOUR_TOKEN_HERE" }
};

await restHelper.DeleteAsync(
    "https://api.example.com/items/456",
    headers
);

Console.WriteLine("Item deleted successfully");
```

### Example 8: HttpClient Extension Methods with Bearer Auth (.NET 5+)

```csharp
#if NET5_0_OR_GREATER
using Nethereum.Util.Rest;
using System.Net.Http;

var httpClient = new HttpClient();
string bearerToken = "your-jwt-token";

// GET with Bearer token
var userData = await httpClient.GetAsync<UserProfile>(
    "https://api.example.com/user/profile",
    bearerToken
);

// POST with Bearer token
var createData = new { name = "New Item", value = 100 };
var createResponse = await httpClient.PostAsync<CreateResponse>(
    "https://api.example.com/items",
    createData,
    bearerToken
);

// PUT with Bearer token
var updateData = new { value = 200 };
var updateResponse = await httpClient.PutAsync<UpdateResponse>(
    "https://api.example.com/items/123",
    updateData,
    bearerToken
);

// DELETE with Bearer token (returns int status)
var deleteStatus = await httpClient.DeleteAsync(
    "https://api.example.com/items/123",
    bearerToken
);
#endif
```

### Example 9: Multipart Form Data for File Uploads

```csharp
using Nethereum.Util.Rest;
using System.Collections.Generic;
using System.Net.Http;

var restHelper = new RestHttpHelper(new HttpClient());

// Create multipart request
var multipartRequest = new MultipartFormDataRequest
{
    Fields = new List<MultipartField>
    {
        new MultipartField { Name = "title", Value = "My Contract" },
        new MultipartField { Name = "network", Value = "mainnet" }
    },
    Files = new List<MultipartFile>
    {
        new MultipartFile
        {
            FieldName = "file",
            FileName = "Token.sol",
            Content = "pragma solidity ^0.8.0; contract Token { ... }",
            ContentType = "text/plain"
        }
    }
};

// Add authorization header
var headers = new Dictionary<string, string>
{
    { "Authorization", "Bearer YOUR_TOKEN" }
};

// Upload file
var uploadResponse = await restHelper.PostMultipartAsync<UploadResponse>(
    "https://api.example.com/upload",
    multipartRequest,
    headers
);

Console.WriteLine($"Upload status: {uploadResponse.Status}");

public class UploadResponse
{
    public string Status { get; set; }
    public string FileId { get; set; }
}
```

## API Reference

### IRestHttpHelper

Generic REST client interface.

```csharp
public interface IRestHttpHelper
{
    // GET request with JSON deserialization
    Task<T> GetAsync<T>(string path, Dictionary<string, string> headers = null);

    // POST request with JSON serialization/deserialization
    Task<TResponse> PostAsync<TResponse, TRequest>(
        string path,
        TRequest request,
        Dictionary<string, string> headers = null);

    // PUT request with JSON serialization/deserialization
    Task<TResponse> PutAsync<TResponse, TRequest>(
        string path,
        TRequest request,
        Dictionary<string, string> headers = null);

    // DELETE request
    Task DeleteAsync(string path, Dictionary<string, string> headers = null);

    // POST multipart form data (file uploads)
    Task<TResponse> PostMultipartAsync<TResponse>(
        string path,
        MultipartFormDataRequest request,
        Dictionary<string, string> headers = null);
}
```

### RestHttpHelper

Concrete implementation of IRestHttpHelper.

```csharp
public class RestHttpHelper : IRestHttpHelper
{
    // Constructor - optionally provide HttpClient
    public RestHttpHelper(HttpClient httpClient = null);

    // Implements all IRestHttpHelper methods
    // Throws Exception on non-success status codes
}
```

### HttpClientExtensions (.NET 5.0+)

Extension methods for HttpClient with Bearer token authentication.

```csharp
public static class HttpClientExtensions
{
    // GET with Bearer token
    public static Task<T> GetAsync<T>(
        this HttpClient httpClient,
        string url,
        string token);

    // POST with Bearer token and response
    public static Task<T> PostAsync<T>(
        this HttpClient httpClient,
        string url,
        object data,
        string token);

    // POST with Bearer token returning HttpResponseMessage
    public static Task<HttpResponseMessage> PostAsync(
        this HttpClient httpClient,
        string url,
        object data,
        string token);

    // PUT with Bearer token
    public static Task<T> PutAsync<T>(
        this HttpClient httpClient,
        string url,
        object data,
        string token);

    // DELETE with Bearer token
    public static Task<int> DeleteAsync(
        this HttpClient httpClient,
        string url,
        string token);
}
```

### MultipartFormDataRequest

Container for multipart form data uploads.

```csharp
public class MultipartFormDataRequest
{
    public List<MultipartField> Fields { get; set; }
    public List<MultipartFile> Files { get; set; }
}

public class MultipartField
{
    public string Name { get; set; }
    public string Value { get; set; }
}

public class MultipartFile
{
    public string FieldName { get; set; }      // Default: "file"
    public string FileName { get; set; }       // e.g., "Token.sol"
    public string Content { get; set; }        // File content as string
    public string ContentType { get; set; }    // Default: "text/plain"
}
```

## Related Packages

### Used By (Consumers)
- **Nethereum.Beaconchain** - Beacon chain API client
- **Nethereum.DataServices** - Data service integrations (Sourcify, 4byte.directory)
- **Nethereum.Consensus.LightClient** - Light client API communication

### Dependencies
- None (foundational package)

## Important Notes

### Error Handling

All methods throw `Exception` on non-success HTTP status codes:

```csharp
try
{
    var data = await restHelper.GetAsync<MyData>("https://api.example.com/data");
}
catch (Exception ex)
{
    // ex.Message contains: "Error: {StatusCode}, {ResponseBody}, {Headers}"
    Console.WriteLine($"API error: {ex.Message}");
}
```

### HttpClient Lifecycle

**Best Practice:** Reuse HttpClient instances to avoid socket exhaustion.

```csharp
// WRONG - creates new HttpClient for every request
public Task<T> GetDataAsync<T>(string url)
{
    var helper = new RestHttpHelper(new HttpClient()); // Don't do this!
    return helper.GetAsync<T>(url);
}

// CORRECT - reuse HttpClient
private readonly IRestHttpHelper _restHelper;

public MyService()
{
    var httpClient = new HttpClient(); // Create once
    _restHelper = new RestHttpHelper(httpClient);
}
```

**Even Better:** Use HttpClientFactory in ASP.NET Core:

```csharp
// Startup.cs or Program.cs
services.AddHttpClient();

// Your service
public class MyService
{
    private readonly IRestHttpHelper _restHelper;

    public MyService(IHttpClientFactory httpClientFactory)
    {
        var httpClient = httpClientFactory.CreateClient();
        _restHelper = new RestHttpHelper(httpClient);
    }
}
```

### JSON Serialization

The package automatically uses the best serializer for your framework:

- **.NET 8+**: `System.Text.Json` (faster, lower memory)
- **Older frameworks**: `Newtonsoft.Json` (compatibility)

This is transparent - you don't need to change your code.

### Case Sensitivity

HttpClientExtensions use case-insensitive JSON deserialization:

```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};
```

This allows matching API responses with different casing conventions.

### Thread Safety

RestHttpHelper is thread-safe when using a shared HttpClient (which is thread-safe). You can safely use a single instance across multiple threads.

## Additional Resources

- [HttpClient Best Practices](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)
- [System.Text.Json Overview](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview)
- [Newtonsoft.Json Documentation](https://www.newtonsoft.com/json/help/html/Introduction.htm)
- [Nethereum Documentation](http://docs.nethereum.com/)
- [RESTful API Guidelines](https://restfulapi.net/)

## License

This package is part of the Nethereum project and follows the same MIT license.
