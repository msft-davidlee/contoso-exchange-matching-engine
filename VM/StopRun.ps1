taskkill /IM Demo.FIXMessageProcessor.exe /F
taskkill /IM Demo.MatchingEngine.exe /F

$Prefix = ""
Start-Process ssh -ArgumentList @("-t", "devuser@$Prefix-cli2","taskkill /IM Demo.CustomerOrder.exe /F && exit")
Start-Process ssh -ArgumentList @("-t", "devuser@$Prefix-cli1","taskkill /IM Demo.CustomerOrder.exe /F && exit")
Start-Process ssh -ArgumentList @("-t", "devuser@$Prefix-mkt1","taskkill /IM Demo.MarketDataRecipient.exe /F && exit")
Start-Process ssh -ArgumentList @("-t", "devuser@$Prefix-mkt2","taskkill /IM Demo.MarketDataRecipient.exe /F && exit")