# Wrapper the overlay uses to launch every action.
#
# Its only job is to make a failure legible. Without it, a script that throws writes a
# multi-line PowerShell error banner to stderr in the OEM code page, and the log ends up with
# a wall of decoration where the accents are mojibake. Here the error is reduced to its
# message and written as explicit UTF-8 bytes.
#
# The overlay reads stderr, so the message lands in atajos.log with a single writer and a
# single encoding.

param(
    [Parameter(Mandatory)]
    [string]$Script
)

$ErrorActionPreference = 'Stop'

try {
    & $Script
}
catch {
    # Bytes straight to the handle on purpose. Windows PowerShell writes its streams in the
    # OEM code page when they are redirected, and setting [Console]::OutputEncoding does not
    # change that, so anything with an accent would arrive corrupted.
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($_.Exception.Message)

    $stderr = [Console]::OpenStandardError()
    $stderr.Write($bytes, 0, $bytes.Length)
    $stderr.Flush()

    exit 1
}
