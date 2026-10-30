$out = Join-Path $PSScriptRoot '.'
$pk = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.cswinrt\2.0.8'
$include = @(
    'Windows.Media.Control',
    'Windows.Foundation',
    'Windows.Media.MediaPlaybackType',
    'Windows.Media.MediaPlaybackAutoRepeatMode',
    'Windows.Storage.Streams.IRandomAccessStreamReference',
    'Windows.Storage.Streams.IRandomAccessStreamWithContentType',
    'Windows.Storage.Streams.IRandomAccessStream',
    'Windows.Storage.Streams.IInputStream',
    'Windows.Storage.Streams.IOutputStream',
    'Windows.Storage.Streams.IBuffer',
    'Windows.Storage.Streams.IContentTypeProvider',
    'Windows.Storage.Streams.DataReader',
    'Windows.Storage.Streams.IDataReader',
    'Windows.Storage.Streams.InputStreamOptions',
    'Windows.Storage.Streams.UnicodeEncoding',
    'Windows.Storage.Streams.ByteOrder'
)
$exclude = @(
    'Windows.Foundation.Diagnostics',
    'Windows.Foundation.Numerics',
    'Windows.Storage.Streams.IRandomAccessStreamReferenceStatics',
    'Windows.Storage.Streams.IBufferFactory',
    'Windows.Storage.Streams.IBufferStatics'
)
$args = @('-input', 'local', '-output', $out, '-target', 'net6.0')
foreach ($i in $include) { $args += @('-include', $i) }
foreach ($e in $exclude) { $args += @('-exclude', $e) }
& (Join-Path $pk 'cswinrt.exe') @args
