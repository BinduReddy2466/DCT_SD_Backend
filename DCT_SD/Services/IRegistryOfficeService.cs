using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Services;

public interface IRegistryOfficeService
{
    Task<IReadOnlyList<RegistryOfficeDto>> GetAllActiveAsync(CancellationToken cancellationToken = default);
}
