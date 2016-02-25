# This is just a sample of how to use the dnurl .net-assembly for fuzzing-like tasks.
param($strPath = "C:\src\dnurl\nurl\bin\Debug\dnurl.exe")

# Create the nurl-object
$assembly = [Reflection.Assembly]::LoadFile($strPath)
$nurl = $assembly.CreateInstance("JamesUtility.DNurl")


# Example of simple automation for google receiving a bad request.
$nurl.strFileName = "requestExample.txt"
$nurl.strHost = "www.google.se"
$nurl.bIsSSL = $false
$nurl.bEchoWrite = $true
$nurl.bDebug = $true
$nurl.port = 80
$nurl.strHttpRequest = "GET / HTTP/1.1\r\nHost: www.google.se\r\n\r\n"

$nurl.run()
$strResponseCode = $nurl.getResponseCode()
$htHeaders = $nurl.getServerHeaders()
$htHeaders
$strBody = $nurl.getServerAsciiBody()
$nurl.closeOutputStreams()

write-host "Response from server: $strResponseCode"
write-host "Body: $strBody "


