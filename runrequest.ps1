# This is just a sample of how to use the dnurl .net-assembly for fuzzing-like tasks.
param($strPath = "dnurl.exe")

# Create the nurl-object
$assembly = [Reflection.Assembly]::LoadFile($strPath)
$nurl = $assembly.CreateInstance("JamesUtility.DNurl")

# Just create some request-file used for tests
echo "GET /INSERTNUMBERHERE HTTP/1.1" > requestExample.txt
echo "Host: justMyHost.com" >> requestExample.txt

# Example of simple automation
$nurl.strFileName = "requestExample.txt"

for ($i = 0 ; $i -lt 10 ; $i = $i + 1)
{
    $nurl.strHost = "127.0.0.1"
    $nurl.bIsSSL = $false
    $nurl.bEchoWrite = $false

    $nurl.strReplacers.Clear()
    $nurl.strReplacers.Add("INSERTNUMBERHERE")
    $nurl.strReplacers.Add($i.ToString())

    $nurl.run()
}

