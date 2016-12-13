namespace vGenWrapper
{
    public enum NtStatus : uint
    {
        Success = 0x00000000,
        ResourceNotOwned = 3221226084,
        DeviceDoesNotExist = 3221225664,
        IoDeviceError = 0xC0000185
    }
}