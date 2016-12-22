rem Create SDK for vGen API 


XCOPY x64\Release\vGenInterface.dll .\SDK\x64\
XCOPY x64\Release\vGenInterface.lib .\SDK\x64\
XCOPY x64\Release\vGenInterface.pdb .\SDK\x64\

XCOPY Win32\Release\vGenInterface.dll .\SDK\x86\
XCOPY Win32\Release\vGenInterface.lib .\SDK\x86\
XCOPY Win32\Release\vGenInterface.pdb .\SDK\x86\

XCOPY .\vGenInterface.h .\SDK\Include\
