# Draftable Compare API - .NET Client Library

This is a thin .NET client for Draftable's [document comparison API](https://draftable.com/comparison-api).
It wraps the available endpoints, and handles authentication and signing for you.
The library is [available on NuGet](https://www.nuget.org/packages/Draftable.CompareAPI.Client/) as `DraftableCompareAPI.Client`.

The examples in this README are all in C#, but any CLR-based language (e.g. F#, VB.NET) is supported.

See the [full API documentation](https://api.draftable.com) for an introduction to the API, usage notes, and other references.

### Getting started

- Sign up for free at [api.draftable.com](https://api.draftable.com) to get your credentials.

- Install the [`Draftable.CompareAPI.Client` NuGet package](https://www.nuget.org/packages/Draftable.CompareAPI.Client/). This will add a reference to `Draftable.CompareAPI.Client`.

- Start creating comparisons:
    ```
    using (var comparisons = new Comparisons("your account id", "your auth token")) {
        var comparison = comparisons.Create(
            Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/left.pdf", "pdf"),
            Comparisons.Side.FromURL("https://api.draftable.com/static/test-documents/paper/right.pdf", "pdf")
        );

        var viewerURL = comparisons.SignedViewerURL(comparison.Identifier, validFor: TimeSpan.FromMinutes(30));

        Console.WriteLine("Comparison created:");
        Console.WriteLine(comparison);
        Console.WriteLine();
        Console.WriteLine($"Viewer URL (expires in 30 min): {viewerURL}");
    }
    ```

-----

# Client API

### Dependencies and supported .NET frameworks
The client depends on the `Newtonsoft.Json` NuGet package for serialization.

The client is built against version 4.5.2 of the .NET framework, so framework versions 4.5.2 and higher are supported.

### Design notes

###### Synchronous and asynchronous requests

All requests can be made synchronously or asynchronously (using the methods suffixed with `Async`). 
All asynchronous methods return `Task`s that, when awaited, will complete succesfully or throw an exception, just like their synchronous counterparts.

###### Errors and error handling

The API is designed such that _requests should always succeed_ and _comparisons should always succeed_ in production. This means:
- Exceptions when making requests will only occur upon network failure, or when you provide invalid credentials or data.
- Comparisons will only fail when the files are unreadable, or exceed your account's size limits.

When making method calls, parameters are immediately validated, and `ArgumentNullException`s and `ArgumentOutOfRangeException`s will be thrown
if you provide invalid parameters. Otherwise, all possible exceptions are documented for every method in the client library.

If you `Dispose()` the `Comparisons` client, further requests will throw an `ObjectDisposedException`, and any requests in progress will be canceled,
throwing an `OperationCanceledException`.

###### Thread safety

The API client class, `Comparisons`, is completely thread-safe.

### Initializing the client

The library provides a namespace `Draftable.CompareAPI` with two classes, `Comparisons` and `Comparison`.

To construct an API client, use `new Comparisons(string accountId, string authToken)`.
The `Comparisons` instance lets you manage your account's comparisons (creating new comparisons, and getting/deleting existing comparisons).
Instances of `Comparison` are returned by API methods, and provide metadata for a given comparison.

Note 1: the simplest `Comparisons` constructor assumes that you are going to communicate with Draftable Cloud API (Cloud API URLs start with `https://api.draftable.com/v1`). If you work with non-cloud Draftable instance (eg. a local self-hosted one), you need to use `Comparisons` constructor with `baseURL` parameter properly specified. This value depends on your local installation and if needed, ask your office Administrator for its value.

Note 2: If you need to customize how HTTP requests are handled (e.g. to use a proxy server), you can use a constructor overload that allows you to configure
the underlying `System.Net.Http.HttpClientHandler` used internally.

Note 3: When communicating with a local Self-Hosted deployment, you may hit SSL errors. You can easily bypass them running: 
```
ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
```
This is an easy way to ignore these errors process-wide. Be careful using that, you should never do this in prod, instead you should properly install SSL certificate in the client machine.

`Comparisons` is disposable. Calling `.Dispose()` will close any underlying connections and otherwise clean up the HTTP communication layer.

So, we'll assume you set things up as follows:

    using Draftable.CompareAPI;
    ...
    var comparisons = new Comparisons(<your account ID>, <your auth token>);

### Getting comparisons

`Comparisons` provides `.GetAll()` and `.Get(string identifier)`.
- `.GetAll()` returns a `List<Comparison>` giving metadata for _all your comparisons_, ordered from newest to oldest. This is a potentially expensive operation.
- `.Get(string identifier)` returns a single `Comparison` object, or raises `Comparisons.NotFoundException` if there isn't a comparison with that identifier.

###### Comparison objects

`Comparison` objects have the following read-only properties:
- `Identifier`: a string giving the identifier.
- `Left`, `Right`: `Comparison.Side` objects giving information about each side, with properties:
    - `FileType`: the file extension.
    - `SourceURL`  _(optional)_: if the file was specified as a URL, this will be a string with the URL. Otherwise, `null`. 
    - `DisplayName` _(optional)_: the display name, if one was given. Otherwise, `null`.
- `IsPublic`: a boolean giving whether the comparison is public, or requires authentication to view.
- `CreationTime`: a `DateTime` giving when the comparison was created.
- `ExpiryTime` _(optional)_: if the comparison will expire, an `DateTime` giving the expiry time. Otherwise, `null` (indicating no expiry).
- `Ready`: boolean indicating whether the comparison is ready for display.

If a `Comparison` is `Ready` (i.e. it has been processed and is ready for display), it the following additional properties will be non-null:
- `ReadyTime`: a `DateTime` giving the time the comparison became ready.
- `Failed`: a boolean indicating whether the comparison succeeded or failed.
- `ErrorMessage` _(only present if `Failed`)_: a string providing the developer with the reason the comparison failed.

###### Example usage

    string identifier = "<identifier>";
    
    try {
        var comparison = comparisons.Get(identifier);
        Debug.Assert(comparison.Identifier == identifier);

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
                Console.WriteLine("The comparison failed. Error message: {0}", comparison.ErrorMessage);
            }
        }

    } catch (Comparisons.NotFoundException) {
        Console.WriteLine("Comparison '{0}' does not exist.", identifier);
    }


### Deleting comparisons

`Comparisons` provides `.Delete(string identifier)`, which attempts to delete the comparison with that identifier.

It has no return value, and raises `Comparisons.NotFoundException` if there isn't a comparison with that identifier. 

###### Example usage

    var allComparisons = comparisons.GetAll();
    var oldestComparisons = allComparisons.OrderBy(comparison => comparison.CreationTime).Take(10).ToList();

    Console.WriteLine("Deleting oldest {0} comparisons...", oldestComparisons.Count);

    foreach (var comparison in oldestComparisons) {
        comparisons.Delete(comparison.Identifier);
        Console.WriteLine("Deleted comparison '{0}'.", comparison.Identifier);
    }

### Creating comparisons

`Comparisons` provides `.Create(left, right, [identifier], [isPublic], [expires])`, which returns
a `Comparison` object representing the newly created comparison.

###### Creation options

`.Create(...)` accepts the following arguments:

- `left`, `right`: `Comparisons.Side` objects describing the left and right files. These are described below.
- `identifier` _(optional)_: the identifier to use for the comparison.
    - If specified, the identifier can't clash with an existing comparison. (If so, a `Comparisons.BadRequestException` is thrown.)
    - If left unspecified, the API will automatically generate one for you.
- `isPublic` _(optional)_: whether the comparison is publicly accessible.
    - Defaults to `false`. If `true`, then the comparison viewer can be accessed by anyone, without authentication.
    - See the full API documentation for details.
- `expires` _(optional)_: an optional `TimeSpan` specifying when the comparison will be automatically deleted.
    - If given, the `TimeSpan` must be positive.
    - Defaults to `null`, meaning the comparison will never expire.

To specify `left` and `right`, create `Comparisons.Side` instances using one of the static constructors.
The full set of overloads are documented in the xml docs, but here are the main ones:

- `Comparisons.Side.FromURL(sourceURL, fileType, [displayName])`
    - Specifies a file via a URL. You must give a fully qualified URL from which Draftable can download the file.
    - `fileType` is required, given as the file extension
    - `displayName` is an optional name for the file, to be shown in the comparison
    
- `Comparisons.Side.FromFile(fileStream, fileType, [displayName])`
    - Specifies a file to be uploaded in the request. You can provide the file as a stream, byte array, or via a file path.
    - `fileType` and `displayName` are as before.

###### Supported file types

The following file types are supported:
- PDF: `pdf`
- Word: `docx`, `docm`, `doc`, `rtf`
- PowerPoint: `pptx`, `pptm`, `ppt`

###### Exceptions

If you try to create a `Comparisons.Side` with an invalid `fileType` or malformed `url`, an `ArgumentOutOfRangeException` will be immediately thrown.

Exceptions are raised by `.Create(...)` if a parameter is invalid (e.g. `expires` is set to a time in the past).
The method will either immediately throw an `ArgumentOutOfRangeException`, or a `Comparisons.BadRequestException` will be thrown after communication with the API.
- Most parameters will be validated client-side by the library, in which case an `ArgumentOutOfRangeException` is thrown.
- If you provide an invalid parameter that isn't validated client-side (e.g. an `identifier` that is already in use by another comparison) then the POST request will fail and a `Comparisons.BadRequestException` will be thrown.

###### Example usage

    var comparison = comparisons.Create(
        Comparisons.Side.FromURL("https://domain.com/path/to/left.pdf", "pdf"),
        Comparisons.Side.FromFile("path/to/right/file.pdf"),
        // identifier: not specified, so Draftable will generate one
        identifier: null,
        // isPublic: false, so that the comparison is private
        isPublic: false,
        // expires: 30 minutes in the future, so the comparison will be automatically deleted then
        expires: TimeSpan.FromMinutes(30)
    );

    Console.WriteLine("Created comparison:");
    Console.WriteLine(comparison);

    // This generates a signed viewer URL that can be used to access the private comparison for the next 10 minutes.
    var viewerURL = comparisons.SignedViewerURL(
        // identifier: The identifier of the comparison
        identifier: comparison.identifier,
        // validFor: The amount of time before the link expires
        validFor: TimeSpan.FromMinutes(10),
        // wait: Whether the viewer should wait for a comparison with the given identifier to exist.
        //       (This is simply `false` for normal usage.)
        wait: false
    );

    Console.WriteLine("Viewer URL (expires in 10 min): {0}", viewerURL);



### Displaying comparisons

Comparisons are displayed using a _viewer URL_. See the section on displaying comparisons in the [API documentation](https://api.draftable.com) for details.

Viewer URLs are generated with the following methods:

- `comparisons.PublicViewerURL(string identifier, bool wait = false)`
    - Viewer URL for a public comparison with the given `identifier`.
    - `wait` is `false` by default, meaning the viewer will 404 and show an error if no such comparison exists.
    - If `wait` is `true`, the viewer will wait for a comparison with the given `identifier` to exist (potentially displaying a loading animation forever).

- `comparisons.SignedViewerURL(String identifier, [TimeSpan validFor], [boolean wait])`
    - Gets a signed viewer URL for a comparison with the given `identifier`. (The signature is an HMAC based on your credentials.)
    - `validFor` gives when the URL will expire.
        - If `validFor` isn't specified, the URL defaults to expiring 30 minutes in the future (more than enough time to load the page). 
    - Again, if `wait` is `true`, the viewer will wait for a comparison with the given `identifier` to exist.


###### Example usage

In this example, we'll start creating a comparison in the background, but immediately direct our user to a viewer.
The comparison viewer will display a loading animation, waiting for the comparison to be created and processed.

    // This generates a unique identifier we can use.
    var identifier = Comparisons.GenerateIdentifier();

    var createComparisonTask = comparisons.CreateAsync(
        Side.FromURL("https://api.draftable.com/static/test-documents/code-of-conduct/left.rtf", "rtf"),
        Side.FromURL("https://api.draftable.com/static/test-documents/code-of-conduct/right.pdf", "pdf"),
        // identifier: specify the identifier we just generated
        identifier: identifier
    );

    // At some point, we will have created the comparison.
    // (The operation could take some time if we're uploading files.)
    // In the mean time, we can immediately give the user a viewer URL, using `wait=true`:
    string viewerURL = comparisons.SignedViewerURL(identifier, TimeSpan.FromMinutes(30), wait: true);

    // This URL is valid for 30 minutes, and will show a loading screen until the comparison is ready.
    Console.WriteLine("Comparison is being created. View it here: {0}", viewerURL);

    // For the purposes of this example, we'll just block until the request finishes.
    var comparison = createComparisonTask.ConfigureAwait(false).GetAwaiter().GetResult();

    // More generally, the async/await pattern is recommended:
    // var comparison = await createComparisonTask;


### Utility methods

- `Comparisons.GenerateIdentifier()` generates a random unique identifier for you to use.


### Proxying and advanced configuration

By default, the client library respects `<system.net>...</system.net>` settings in your app's configuration file, as well as any system-wide internet settings (e.g. proxy server) set in `Internet Options`.

If you need to customize request settings, you can add settings to application config (e.g. see [this MSDN page](https://docs.microsoft.com/en-us/dotnet/framework/network-programming/proxy-configuration) for proxy configuration). Alternatively, you can use a constructor for `Comparisons` that takes in a configuration callback.

The configuration callback is an `Action<HttpClientHandler>` that can perform any necessary configuration of the client library's underlying `HttpClientHandler`, including setting proxy settings, timeouts, or other request parameters.

-----

That's it! Please report issues you encounter, and we'll work quickly to resolve them. Contact us at [support@draftable.com](mailto://support@draftable.com) if you need assistance.
