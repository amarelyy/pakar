using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Pakar.Shared.Geometry;
using Pakar.Web.Services;

namespace Pakar.Web.Tests;

public class UserSessionTests
{
    private sealed class StorageJs : IJSRuntime
    {
        public string? Value;
        public int Reads;
        public TaskCompletionSource Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => InvokeAsync<T>(identifier, default, args);
        public async ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args)
        {
            if (identifier.EndsWith("setItem")) Value = (string)args![1]!;
            if (identifier.EndsWith("getItem")) { Reads++; await Gate.Task; return (T)(object)Value!; }
            return default!;
        }
    }

    [Fact]
    public async Task ConcurrentComponentsWaitForTheSameSessionRestore()
    {
        var js = new StorageJs();
        var storage = new ProtectedSessionStorage(js, new EphemeralDataProtectionProvider());
        var expected = new SignedInUser("test-token", DateTime.UtcNow.AddHours(1), Guid.NewGuid(), "Admin", "test@example.test", "Admin");
        await storage.SetAsync("auth-session", expected);
        var session = new UserSession(storage, TimeProvider.System);
        var first = session.GetUserAsync().AsTask();
        var second = session.GetUserAsync().AsTask();
        Assert.False(second.IsCompleted);
        js.Gate.SetResult();
        var users = await Task.WhenAll(first, second);
        Assert.All(users, user => Assert.Equal(expected, user));
        Assert.Equal(1, js.Reads);
    }

    [Fact]
    public void ImageDimensionsRejectTruncationAndReadPngAspectRatio()
    {
        byte[] bytes = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), 1280);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), 720);
        Assert.Equal((1280, 720), ImageDimensions.Read(bytes));
        Assert.Null(ImageDimensions.Read(bytes.AsSpan(0, 20)));
    }
}
