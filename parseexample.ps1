# This is just a sample of how to use the dnurl .net-assembly for fuzzing-like tasks.
param($strPath = "dnurl.exe")

# Create the nurl-object
$assembly = [Reflection.Assembly]::LoadFile($strPath)
$nurl = $assembly.CreateInstance("JamesUtility.DNurl")

# Just create some request-file used for tests
echo "GET / HTTP/1.1" > requestExample.txt
echo "Host: www.google.com" >> requestExample.txt

# Example of simple automation for google receiving a bad request.
$nurl.strFileName = "requestExample.txt"
$nurl.strHost = 173.194.122.207
$nurl.bIsSSL = $false
$nurl.bEchoWrite = $false
$nurl.port = 80

$nurl.run()
$strResponseCode = $nurl.getResponseCode()
$htHeaders = $nurl.getServerHeaders()
$htHeaders
$strBody = $nurl.getServerAsciiBody()
$nurl.closeOutputStreams()

write-host "Response from server: $strResponseCode"
write-host "Body: $strBody "


