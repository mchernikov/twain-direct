:: 
:: Debug...
::
if exist %~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Debug\TwainDirect.Scanner.exe (
	cd "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Debug"
	start /b TwainDirect.Scanner.exe mode=window command=start confirmscan scale=1
	exit
)

:: 
:: Release...
::
if exist %~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Release\TwainDirect.Scanner.exe (
	cd "%~dp0\..\..\source\TwainDirect.Scanner\bin\x86\Release"
	start /b TwainDirect.Scanner.exe mode=window command=start confirmscan scale=1
	exit
)
