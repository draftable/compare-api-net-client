Draftable Compare API - .NET Client Library
===========================================

[![nuget ver](https://img.shields.io/nuget/v/Draftable.CompareAPI.Client)](https://www.nuget.org/packages/Draftable.CompareAPI.Client)
[![nuget dlt](https://img.shields.io/nuget/dt/Draftable.CompareAPI.Client)](https://www.nuget.org/packages/Draftable.CompareAPI.Client)
[![license](https://img.shields.io/github/license/draftable/compare-api-net-client)](https://choosealicense.com/licenses/mit/)

A thin .NET client for the [Draftable API](https://draftable.com/rest-api) which wraps all available endpoints and handles authentication and signing.

The documentation and subsequent examples are provided for C#, however, any CLR-based language is supported (e.g. F#, PowerShell, VB.NET).

See the [full API documentation](https://api.draftable.com) for an introduction to the API, usage notes, and other reference material.

Requirements
------------

- Operating system: Any maintained Linux, macOS, or Windows release
- .NET runtime: .NET Framework 4.5.2+ (Windows only) or .NET Core 2.1+

Getting started
---------------

- Create a free [API account](https://api.draftable.com)
- Retrieve your [credentials](https://api.draftable.com/account/credentials)
- Add the [Draftable.CompareAPI.Client](https://www.nuget.org/packages/Draftable.CompareAPI.Client) library
- Start creating comparisons

```csharp
using (var comparisons = new Comparisons("<yourAccountId>", "<yourAuthToken>")) {
    var comparison = comparisons.Create(
        Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/left.pdf", "pdf"),
        Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/right.pdf", "pdf")
    );
    Console.WriteLine($"Comparison created: {comparison}");

    // Generate a signed viewer URL to access the private comparison. The expiry
    // time defaults to 30 minutes if the ValidFor parameter is not provided.
    var viewerURL = comparisons.SignedViewerURL(comparison.Identifier);
    Console.WriteLine($"Viewer URL (expires in 30 mins): {viewerURL}");
}
```

API reference
-------------

### Design notes

#### Exceptions and error handling

Method calls immediately validate parameters. The following exceptions are thrown on validation failure:

- `ArgumentNullException`  
  A required non-null parameter was not provided.
- `ArgumentOutOfRangeException`  
  A required parameter contained invalid data.

Disposing the `Comparisons` client results in subsequent requests throwing `ObjectDisposedException`. Any in-progress requests will be cancelled and throw `OperationCanceledException`.

#### Synchronous and asynchronous requests

- Requests may be made synchronously, or asynchronously using the methods suffixed with `Async`.
- Asynchronous methods return a `Task`, which when awaited, will complete succesfully or throw an exception.

#### Thread safety

The API client class, `Comparisons`, is thread-safe.

### Initializing the client

The package provides a namespace, `Draftable.CompareAPI`, with which a `Comparisons` instance can be created for your API account.

`Comparisons` provides methods to manage the comparisons for your API account and return individual `Comparison` objects.

Creating a `Comparisons` instance differs slightly based on the API endpoint being used:

```csharp
using Draftable.CompareAPI;

// Draftable API (default endpoint)
var comparisons = new Comparisons(
    "<yourAccountId>",  // Replace with your API credentials from:
    "<yourAuthToken>"   // https://api.draftable.com/account/credentials
);

// Draftable API regional endpoint or Self-hosted
var comparisons = new Comparisons(
    "<yourAccountId>",  // Replace with your API credentials from the regional
    "<yourAuthToken>",  // Draftable API endpoint or your Self-hosted container
    'https://draftable.example.com/api/v1'  // Replace with the endpoint URL
);
```

The `Comparisons` instance can be disposed by calling `Dispose()`.

For API Self-hosted you may need to [suppress TLS certificate validation](#self-signed-certificates) if the server is using a self-signed certificate (the default).

### Retrieving comparisons

- `GetAll()`  
  Returns a `List<Comparison>` of all your comparisons, ordered from newest to oldest. This is potentially an expensive operation.
- `Get(string identifier)`  
  Returns the specified `Comparison` or raises a `NotFoundException` exception if the specified comparison identifier does not exist.

`Comparison` objects have the following properties:

- `Identifier: string`  
  The unique identifier of the comparison
- `Left: Comparison.Side` / `Right: Comparison.Side`  
  Information about each side of the comparison
  - `FileType: string`  
    The file extension
  - `SourceURL: string` _(optional)_  
    The URL for the file if the original request was specified by URL, otherwise `null`
  - `DisplayName: string` _(optional)_  
    The display name for the file if given in the original request, otherwise `null`
- `IsPublic: bool`  
  Indicates if the comparison is public
- `CreationTime: DateTime`  
  Time in UTC when the comparison was created
- `ExpiryTime: DateTime` _(optional)_  
  The expiry time if the comparison is set to expire, otherwise `null`
- `Ready: bool`  
  Indicates if the comparison is ready to display

If a `Comparison` is _ready_ (i.e. it has been processed) it has the following additional properties:

- `ReadyTime: DateTime`  
  Time in UTC the comparison became ready
- `Failed: bool`  
  Indicates if comparison processing failed
- `ErrorMessage: string` _(only present if `Failed`)_  
  Reason processing of the comparison failed

#### Example usage

```csharp
string identifier = "<identifier>";

try {
    var comparison = comparisons.Get(identifier);

    Console.WriteLine(
        "Comparison '{0}' ({1}) is {2}.",
        identifier,
        comparison.IsPublic ? "public" : "private",
        comparison.Ready ? "ready" : "not ready"
    );

    if (comparison.Ready) {
        Console.WriteLine(
            "The comparison took {0} seconds.",
            (comparison.ReadyTime.Value - comparison.CreationTime).TotalSeconds
        );

        if (comparison.Failed.Value) {
            Console.WriteLine(
                "The comparison failed with error: {0}",
                comparison.ErrorMessage
            );
        }
    }
} catch (Comparisons.NotFoundException) {
    Console.WriteLine("Comparison '{0}' does not exist.", identifier);
}
```

### Deleting comparisons

- `Delete(string identifier)`  
  Returns nothing on successfully deleting the specified comparison or raises a `NotFoundException` exception if no such comparison exists.

#### Example usage

```csharp
var allComparisons = comparisons.GetAll();
var oldestComparisons = allComparisons.OrderBy(comparison => comparison.CreationTime).Take(10).ToList();
Console.WriteLine("Deleting oldest {0} comparisons ...", oldestComparisons.Count);

foreach (var comparison in oldestComparisons) {
    comparisons.Delete(comparison.Identifier);
    Console.WriteLine("Comparison '{0}' deleted.", comparison.Identifier);
}
```

### Creating comparisons

- `Create(Comparisons.Side left, Comparisons.Side right, string identifier = null, bool isPublic = false, TimeSpan expires = null)`  
  Returns a `Comparison` representing the newly created comparison.

`Create` accepts the following arguments:

- `left` / `right`  
  Describes the left and right files (see following section)
- `identifier` _(optional)_  
  Identifier to use for the comparison:
  - If specified, the identifier must be unique (i.e. not already be in use)
  - If unspecified or `null`, the API will automatically generate a unique identifier
- `isPublic` _(optional)_  
  Specifies the comparison visibility:
  - If `false` or unspecified authentication is required to view the comparison
  - If `true` the comparison can be accessed by anyone with knowledge of the URL
- `expires` _(optional)_  
  Time at which the comparison will be deleted:
  - If specified, the provided expiry time must be UTC and in the future
  - If unspecified or `null`, the comparison will never expire (but may be explicitly deleted)

The following exceptions may be raised in addition to [parameter validation exceptions](#exceptions-and-error-handling):

- `BadRequestException`  
  The request could not be processed (e.g. `identifier` already in use)

#### Creating comparison sides

The two most common static constructors for creating `Comparisons.Side` objects are:

- `Comparisons.Side.FromFile(Stream fileStream, string fileType, string displayName = null)`  
  Returns a `Comparisons.Side` for a locally accessible file.
- `Comparisons.Side.FromURL(string sourceURL, string fileType, string displayName = null)`  
  Returns a `Comparisons.Side` for a remotely accessible file referenced by URL.

These constructors accept the following arguments:

- `fileStream` _(`FromFile` only)_  
  A file object to be read and uploaded
  - The file must be opened for reading in _binary mode_
- `sourceURL` _(`FromURL` only)_  
  The URL from which the server will download the file
- `fileType`  
  The type of file being submitted:
  - PDF: `pdf`
  - Word: `docx`, `docm`, `doc`, `rtf`
  - PowerPoint: `pptx`, `pptm`, `ppt`
- `displayName` _(optional)_  
  The name of the file shown in the comparison viewer

#### Example usage

```csharp
var comparison = comparisons.Create(
    Comparisons.Side.FromURL("https://domain.com/path/to/left.pdf", "pdf"),
    Comparisons.Side.FromFile("path/to/right/file.docx", "docx"),
    // Expire this comparison in 2 hours (default is no expiry)
    expires: TimeSpan.FromHours(2)
);
Console.WriteLine($"Created comparison: {comparison}");
```

### Displaying comparisons

- `PublicViewerURL(string identifier, bool wait = false)`  
  Generates a public viewer URL for the specified comparison
- `SignedViewerURL(string identifier, TimeSpan validFor = null, bool wait = false)`  
  Generates a signed viewer URL for the specified comparison

Both methods use the following common parameters:

- `identifier`  
  Identifier of the comparison for which to generate a _viewer URL_
- `wait` _(optional)_  
  Specifies the behaviour of the viewer if the provided comparison does not exist
  - If `false` or unspecified, the viewer will show an error if the `identifier` does not exist
  - If `true`, the viewer will wait for a comparison with the provided `identifier` to exist  
    Note this will result in a perpetual loading animation if the `identifier` is never created

The `SignedViewerURL` method also supports the following parameters:

- `validFor` _(optional)_  
  Time at which the URL will expire (no longer load)
  - If specified, the provided expiry time must be UTC and in the future
  - If unspecified, the URL will be generated with the default 30 minute expiry

See the displaying comparisons section in the [API documentation](https://api.draftable.com) for additional details.

#### Example usage

```csharp
var identifier = '<identifier>';

// Retrieve a signed viewer URL which is valid for 1 hour. The viewer will wait
// for the comparison to exist in the event processing has not yet completed.
string viewerUrl = comparisons.SignedViewerURL(identifier, TimeSpan.FromHours(1), wait: true);
Console.WriteLine($"Viewer URL (expires in 1 hour): {viewerUrl}");
```

### Utility methods

- `GenerateIdentifier()`
  Generates a random unique comparison identifier

Other information
-----------------

### Network & proxy configuration

The library respects any [Network Settings](https://docs.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/network/system-net-element-network-settings) defined in your application's configuration file, as well as any operating system proxy server configuration (e.g. as configured in _Internet Settings_).

In addition, the `Comparisons` class provides a constructor which allows for customisation of the `Net.Http.HttpClientHandler` instance used internally via an `Action<HttpClientHandler>` callback.

### Self-signed certificates

If connecting to an API Self-hosted endpoint which is using a self-signed certificate (the default) you will need to suppress certificate validation. The recommended approach is to import the self-signed certificate into the certificate store of your operating system, which will ensure the .NET runtime trusts the certificate.

Alternatively, you can suppress certificate validation by providing a server certificate validation callback to the `ServicePointManager` instance. The simplest implementation is to disable all certificate validation for all TLS connections in the process. For example:

```csharp
ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
```

Disabling certificate validation in production environments is strongly discouraged as it significantly lowers security. We only recommend using this approach in development environments if configuring a CA signed certificate for API Self-hosted is not possible.
