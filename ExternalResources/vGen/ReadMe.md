#General
vGenInterface is a collection of 3 APIs that can be used for writing a feeder for vXbox devices and for a mixture of vXbox/vJoy devices. It is also possible to use it for writing a vJoy-only feeder but it is currently recommended to use vJoyInterface API for that purpose.
vGenInterface offers three sets of interface functions:

1. Common API: Equally useful for vJoy and vXbox.
1. vJoy API: Backward Compatible with vJoyInterface. Also supports vXbox.
1. vXbox API: Tailored for vXbox feeders. Cannot be used for vJoy devices.

You can use a mixture of the above APIs if needed.

##Which API
Each API has it advantages and disadvantages. In addition, you can mix them without difficulty. You should first decide which kind of feeder you are writing:

###vJoy Only:
If your feeder is designed to feed only vJoy devices, you should not use this API. Use vJoyInterface.dll instead.

###Convert a Feeder:
Your feeder is written for vJoy devices and you use API file vJoyInterface.dll.
By linking to this (vGenInterface.dll) API you should get exactly the same results when target devices are vJoy devices.
If you want to add support for vXbox devices, it is recommended that you make at least the following minimal changes in your code:

1. Convert vXbox device index values to range 1001-1004
2. Report to the user the LED number of the vXbox device.

Additional changes you might introduce to improve your feeder:

1. Use vXbox virtual bus administrative functions:
  1. Call function isVBusExist() to verify that the system supports vXbox.
  2. Call function GetNumEmptyBusSlots() to see if you can add a new vXbox device.
2. If you want to support FFB add an FFB thread and poll Vibration status by repeatedly calling GetVibration() and passing vibration values to your hardware.

###New Feeders:
If the new feeder is designed for only one of the target types, you can safely use either vXbox API  or vJoyInterface.dll.
If the new feeder is designed to support both vJoy and vXbox, you should decide whether you want to take the agnostic approach or not.

__Agnostic__: Most operations will treat devices regardless to their type (vJoy/vXbox). 
This approach simplifies the code but does not take advantage of the specifics of each type. 
Example: you might need to check if button 10 exists before using it though it always exists for vXbox device.

__Non-Agnostic__: Most operations will treat devices according to their type (vJoy/vXbox).
This approach requires per-type coding making the code larger. However, it will enable the code to take advantage of the particular features of each type.
Example: You might take advantage of the large number of buttons in vJoy devices.

__Note__:  It is OK to mix the approaches in your feeder code. 
