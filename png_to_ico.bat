@echo off

set PATH = %PATH%;C:\Program Files\ImageMagick-6.9.10-Q8

cd C:\work\test\icons

convert.exe server16.png server32.png server48.png server64.png server128.png server256.png fish_service.ico