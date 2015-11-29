using HidSharp;

namespace MFIGamepadShared
{
    public class HidDeviceRepresentation
    {
        public HidDeviceRepresentation()
        {
        }

        public HidDeviceRepresentation(HidDevice hidDevice)
        {
            Manufacturer = hidDevice.Manufacturer;
            ProductName = hidDevice.ProductName;
            VendorId = hidDevice.VendorID;
            ProductVersion = hidDevice.ProductVersion;
            ProductId = hidDevice.ProductID;
            SerialNumber = hidDevice.SerialNumber;
        }

        public string Manufacturer { get; set; }
        public string ProductName { get; set; }
        public int? VendorId { get; set; }
        public int? ProductVersion { get; set; }
        public int? ProductId { get; set; }
        public string SerialNumber { get; set; }

        public override bool Equals(object obj)
        {
            return ToString() == obj.ToString();
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return
                $"{Manufacturer} {ProductName} (Vendor ID: {VendorId}, Product ID: {ProductId}, Product version: {ProductVersion}, Serial Number: {SerialNumber})";
        }
    }
}