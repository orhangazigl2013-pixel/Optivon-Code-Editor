#include "c++compiler.hpp"
#include <string>
#include <shlwapi.h>

#pragma comment(lib, "Shlwapi.lib")

void RunCode(HWND hwndOwner, const wchar_t* filePath) {
    // 1. Kaydedilmemiş dosya kontrolü
    if (filePath == NULL || filePath[0] == L'\0') {
        MessageBoxW(hwndOwner, L"Kodu çalıştırmadan önce lütfen dosyayı kaydedin (Ctrl+S)!", L"Optivon Code Editor", MB_OK | MB_ICONWARNING);
        return;
    }

    std::wstring path(filePath);
    std::wstring ext = PathFindExtensionW(filePath);
    std::wstring command = L"";

    // Dosyanın bulunduğu dizini ve dosya adını ayıkla
    wchar_t dirBuffer[MAX_PATH];
    wcscpy_s(dirBuffer, filePath);
    PathRemoveFileSpecW(dirBuffer);

    // 2. Dil Uzantısına Göre Derleme/Çalıştırma Komutu Hazırlama
    if (ext == L".cpp" || ext == L".c") {
        // C/C++: w64devkit / g++ ile derle ve çalıştır
        // NOT: w64devkit klasörün varsa yolunu "tools/w64devkit/bin/g++.exe" yapabilirsin
        command = L"cmd.exe /k \"g++ \"" + path + L"\" -o \"" + path + L".exe\" && \"" + path + L".exe\"\"";
    } 
    else if (ext == L".cs") {
        // C#: Windows yerleşik .NET csc.exe derleyicisi
        std::wstring cscPath = L"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe";
        command = L"cmd.exe /k \"\"" + cscPath + L"\" /out:\"" + path + L".exe\" \"" + path + L"\" && \"" + path + L".exe\"\"";
    } 
    else if (ext == L".py") {
        // Python yorumlayıcısı
        command = L"cmd.exe /k \"python \"" + path + L"\"\"";
    } 
    else if (ext == L".js") {
        // Node.js
        command = L"cmd.exe /k \"node \"" + path + L"\"\"";
    } 
    else {
        MessageBoxW(hwndOwner, L"Desteklenmeyen dosya türü!", L"Optivon Code Editor", MB_OK | MB_ICONERROR);
        return;
    }

    // 3. Komutu Yeni Bir Siyah Konsol Penceresinde Çalıştır
    STARTUPINFOW si = { sizeof(STARTUPINFOW) };
    PROCESS_INFORMATION pi;

    if (CreateProcessW(
        NULL, 
        &command[0], 
        NULL, NULL, FALSE, 
        CREATE_NEW_CONSOLE, 
        NULL, 
        dirBuffer, 
        &si, &pi)) 
    {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
    } else {
        MessageBoxW(hwndOwner, L"Çalıştırma işlemi başlatılamadı!", L"Optivon Code Editor", MB_OK | MB_ICONERROR);
    }
}