using DCT_SD.Models;
using DCT_SD.Models.Dtos.EmptyFolders;

namespace DCT_SD.Services;

public interface IEmptyFolderService
{
    Task<PagedResult<EmptyFolderListItemDto>> SearchAsync(EmptyFolderSearchRequestDto request, CancellationToken cancellationToken = default);
}
