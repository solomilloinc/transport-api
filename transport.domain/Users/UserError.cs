using Transport.SharedKernel;

namespace Transport.Domain.Users;

public static class UserError
{
    public static readonly Error NotFound = Error.NotFound(
        "User.NotFound",
        "No se encontró el usuario especificado.");

    public static readonly Error Inactive = Error.Validation(
        "User.Inactive",
        "El usuario se encuentra inactivo.");

    public static readonly Error EmailAlreadyExists = Error.Conflict(
        "User.EmailAlreadyExists",
        "Ya existe un usuario con ese correo electrónico.");

    public static readonly Error CustomerEmailConflict = Error.Conflict(
        "User.CustomerEmailConflict",
        "Existe más de un cliente con ese correo electrónico. No se pudo vincular automáticamente.");

    public static readonly Error CustomerAlreadyLinked = Error.Conflict(
        "User.CustomerAlreadyLinked",
        "El cliente ya se encuentra vinculado a otra cuenta.");

    public static readonly Error InvalidOperativeRole = Error.Validation(
        "User.InvalidOperativeRole",
        "Solo se permite crear o actualizar usuarios operativos con rol User.");

    public static readonly Error InvalidRole = Error.Validation(
        "User.InvalidRole",
        "El rol indicado no es válido.");

    public static readonly Error GoogleEmailNotVerified = Error.Validation(
        "User.GoogleEmailNotVerified",
        "La cuenta de Google no tiene un correo verificado.");

    public static readonly Error ProfileNotAvailable = Error.NotFound(
        "User.ProfileNotAvailable",
        "No se pudo obtener el perfil del usuario actual.");

    public static readonly Error ClientProfileMissing = Error.Validation(
        "User.ClientProfileMissing",
        "La cuenta no tiene un cliente vinculado para completar el perfil.");

    public static readonly Error DocumentAlreadyExists = Error.Conflict(
        "User.DocumentAlreadyExists",
        "Ya existe un cliente con ese número de documento.");

    public static readonly Error GoogleRestrictedRole = Error.Validation(
        "User.GoogleRestrictedRole",
        "Ese correo ya pertenece a una cuenta administrativa u operativa. Ingresá con email y contraseña.");
}
