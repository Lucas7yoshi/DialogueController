@echo off
pushd Client
dotnet publish -c Debug
popd

pushd Server
dotnet publish -c Debug
popd

rmdir /s /q dist
mkdir dist

copy /y fxmanifest.lua dist
xcopy /y /e Client\bin\Debug\net452\publish dist\Client\bin\
xcopy /y /e Server\bin\Debug\netstandard2.0\publish dist\Server\bin\