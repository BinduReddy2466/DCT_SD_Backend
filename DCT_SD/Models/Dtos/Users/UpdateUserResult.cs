namespace DCT_SD.Models.Dtos.Users;

// Carries the pre-update Status alongside the updated record, so UsersController.Edit (POST) can
// decide whether to send a status-change email without a second GetByIdAsync round-trip to the
// same row - UpdateAsync already has the entity loaded (and its Status not yet overwritten) at
// the point it would otherwise need to be fetched again.
public record UpdateUserResult(UserDetailDto User, string PreviousStatus);
