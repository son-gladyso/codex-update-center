using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace UpdateCenter.Tests;

public sealed class NativeDependencyTests
{
    [Fact] public async Task PartialNativeTransferResumesFromExactOffset()
    {
        using var f = new Fixture(); var path = f.File("native.tgz.partial", [1,2]); path=path[..^8];
        using var network = new OfficialNetwork(new Handler(request =>
        {
            Assert.Equal(2, request.Headers.Range!.Ranges.Single().From);
            Assert.NotNull(request.Headers.Range.Ranges.Single().To);
            var response=new HttpResponseMessage(HttpStatusCode.PartialContent) {Content=new ByteArrayContent([3,4])};
            response.Content.Headers.ContentRange=new ContentRangeHeaderValue(2,3,4); return response;
        }));
        await NativeDependency.DownloadAsync(network,"https://registry.npmjs.org/native",path,default);
        Assert.Equal(new byte[] {1,2,3,4},await File.ReadAllBytesAsync(path));
    }
    [Fact] public async Task WrongNativeRangeCannotOverwritePrefix()
    {
        using var f=new Fixture(); var path=f.File("native.tgz.partial",[1,2]); path=path[..^8];
        using var network=new OfficialNetwork(new Handler(_=>
        {
            var response=new HttpResponseMessage(HttpStatusCode.PartialContent) {Content=new ByteArrayContent([3,4])};
            response.Content.Headers.ContentRange=new ContentRangeHeaderValue(1,2,4); return response;
        }));
        await Assert.ThrowsAsync<InvalidDataException>(()=>NativeDependency.DownloadAsync(network,"https://registry.npmjs.org/native",path,default));
        Assert.False(File.Exists(path)); Assert.Equal(new byte[] {1,2},await File.ReadAllBytesAsync(path+".partial"));
    }
    [Fact] public async Task CompletedNativePartialCanRecoverAfterLastWrite()
    {
        using var f=new Fixture(); var path=f.File("native.tgz.partial",[1,2]); path=path[..^8];
        using var network=new OfficialNetwork(new Handler(_=>
        {
            var response=new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
            response.Content.Headers.ContentRange=new ContentRangeHeaderValue(2); return response;
        }));
        await NativeDependency.DownloadAsync(network,"https://registry.npmjs.org/native",path,default);
        Assert.Equal(new byte[] {1,2},await File.ReadAllBytesAsync(path));
    }
}
