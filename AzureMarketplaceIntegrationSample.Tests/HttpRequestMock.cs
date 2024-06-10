using Microsoft.AspNetCore.Http;
using System.Text;

namespace AzureMarketplaceIntegrationSample.Tests;

internal class HttpRequestMock : HttpRequest
{
    private readonly string _body;
    private readonly QueryCollection _query;
    private readonly HttpContext _context;
    private readonly IHeaderDictionary _headers;
    public HttpRequestMock(string body, QueryCollection query = null, IHeaderDictionary headers = null)
    {
        _body = body;
        var mock = new Moq.Mock<HttpContext>(Moq.MockBehavior.Strict);
        mock.Setup(x => x.Request).Returns(this);
        mock.Setup(x => x.RequestServices).Returns<IServiceProvider>(null);
        _context = mock.Object;
        _query = query;
        _headers = headers;
    }

    public override HttpContext HttpContext => _context;

    public override string Method { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override string Scheme { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override bool IsHttps { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override HostString Host { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override PathString PathBase { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override PathString Path { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override QueryString QueryString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override IQueryCollection Query
    {
        get => _query;
        set => throw new NotImplementedException();
    }
    public override string Protocol { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override IHeaderDictionary Headers => _headers;

    public override IRequestCookieCollection Cookies { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override long? ContentLength { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override string ContentType { get => "application/json"; set => throw new NotImplementedException(); }
    public override Stream Body { get => new MemoryStream(Encoding.UTF8.GetBytes(_body)); set => throw new NotImplementedException(); }

    public override bool HasFormContentType => throw new NotImplementedException();

    public override IFormCollection Form { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}