using FSH.Framework.Storage.DTOs;

namespace FSH.Framework.Storage.Services;

public interface IStorageService
{
    Task<string> UploadAsync<T>(
        FileUploadRequest request,
        FileType fileType,
        CancellationToken cancellationToken = default) where T : class;

    Task RemoveAsync(string path, CancellationToken cancellationToken = default);
}