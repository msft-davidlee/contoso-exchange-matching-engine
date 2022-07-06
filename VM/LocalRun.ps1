# Copy to desktop on trading platform VM.

Push-Location C:\dev\fixmsgprocessor
Start-Process -FilePath Demo.FIXMessageProcessor.exe
Pop-Location

Push-Location C:\dev\matchingengine\
Start-Process -FilePath Demo.MatchingEngine.exe
Pop-Location

$Prefix = ""
Start-Process ssh -ArgumentList @("-t", "devuser@$Prefix-cli2","cd C:\dev\client2 && powershell")
Start-Process ssh -ArgumentList @("-t", "devuser@$Prefix-cli1","cd C:\dev\client1 && powershell")
Start-Process ssh -ArgumentList @("-t", "devuser@$Prefix-mkt1","cd C:\dev\marketdata1 && powershell")
Start-Process ssh -ArgumentList @("-t", "devuser@$Prefix-mkt2","cd C:\dev\marketdata2 && powershell")