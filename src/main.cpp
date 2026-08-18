#include <windows.h>
#include <richedit.h>
#include <shlwapi.h>
#include <shellapi.h> // Birlikte Aç ve Sürükle-Bırak için gerekli
#include <dwmapi.h>
#include <string>
#include <vector>
#include <fstream>
#include "c++compiler.hpp"

// Windows 11 / 10 Modern Görünüm ve Dark Mode
#pragma comment(linker, "\"/manifestdependency:type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")
#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "shell32.lib")

#ifndef DWMWA_USE_IMMERSIVE_DARK_MODE
#define DWMWA_USE_IMMERSIVE_DARK_MODE 20
#endif

#define IDC_MAIN_EDIT 101

#define ID_FILE_NEW       2001
#define ID_FILE_OPEN      2002
#define ID_FILE_SAVE      2003
#define ID_FILE_SAVEAS    2004
#define ID_FILE_EXIT      2005
#define ID_CODE_RUN       2006

COLORREF bgEditColor = RGB(30, 30, 30);      
COLORREF fgEditColor = RGB(220, 220, 220);  
COLORREF keywordColor = RGB(86, 156, 214);  

WNDPROC oldEditProc = NULL;
bool isFormatting = false;
HWND g_hEdit = NULL;
HWND g_hwndMain = NULL;
wchar_t g_currentFilePath[MAX_PATH] = { 0 };
wchar_t g_initialFilePath[MAX_PATH] = { 0 }; // Birlikte Aç ile gelen dosya yolu

void HighlightSyntax(HWND hEdit) {
    if (isFormatting) return;
    isFormatting = true;

    SendMessage(hEdit, WM_SETREDRAW, FALSE, 0);

    CHARRANGE sel;
    SendMessage(hEdit, EM_EXGETSEL, 0, (LPARAM)&sel);

    int textLength = GetWindowTextLengthW(hEdit);
    if (textLength > 0) {
        std::vector<wchar_t> buffer(textLength + 1);
        GetWindowTextW(hEdit, buffer.data(), textLength + 1);
        std::wstring content(buffer.data());

        CHARFORMAT2W cfDefault = { sizeof(CHARFORMAT2W) };
        cfDefault.cbSize = sizeof(CHARFORMAT2W);
        cfDefault.dwMask = CFM_COLOR;
        cfDefault.crTextColor = fgEditColor;
        SendMessage(hEdit, EM_SETSEL, 0, -1);
        SendMessage(hEdit, EM_SETCHARFORMAT, SCF_SELECTION, (LPARAM)&cfDefault);

        std::vector<std::wstring> keywords = {
            L"int", L"float", L"double", L"char", L"void", L"bool", L"string", L"auto",
            L"return", L"class", L"struct", L"enum", L"union", L"interface", L"namespace",
            L"public", L"private", L"protected", L"virtual", L"override", L"static", L"const",
            L"if", L"else", L"for", L"while", L"do", L"switch", L"case", L"break", L"continue",
            L"using", L"include", L"import", L"export", L"from", L"default", L"extends",
            L"def", L"function", L"fn", L"func", L"var", L"let", L"async", L"await",
            L"try", L"catch", L"finally", L"throw", L"raise", L"new", L"delete",
            L"true", L"false", L"null", L"nullptr", L"None", L"undefined", L"print", L"std"
        };

        for (const auto& kw : keywords) {
            size_t pos = content.find(kw);
            while (pos != std::wstring::npos) {
                bool leftOk = (pos == 0) || (!iswalnum(content[pos - 1]) && content[pos - 1] != L'_');
                bool rightOk = (pos + kw.length() >= content.length()) || (!iswalnum(content[pos + kw.length()]) && content[pos + kw.length()] != L'_');

                if (leftOk && rightOk) {
                    SendMessage(hEdit, EM_SETSEL, pos, pos + kw.length());
                    CHARFORMAT2W cfKeyword = { sizeof(CHARFORMAT2W) };
                    cfKeyword.cbSize = sizeof(CHARFORMAT2W);
                    cfKeyword.dwMask = CFM_COLOR;
                    cfKeyword.crTextColor = keywordColor;
                    SendMessage(hEdit, EM_SETCHARFORMAT, SCF_SELECTION, (LPARAM)&cfKeyword);
                }
                pos = content.find(kw, pos + kw.length());
            }
        }
    }

    SendMessage(hEdit, EM_EXSETSEL, 0, (LPARAM)&sel);
    SendMessage(hEdit, WM_SETREDRAW, TRUE, 0);
    RedrawWindow(hEdit, NULL, NULL, RDW_ERASE | RDW_FRAME | RDW_INVALIDATE | RDW_ALLCHILDREN);

    isFormatting = false;
}

// Dosya Yükleme Fonksiyonu
bool LoadFileToEdit(HWND hEdit, const wchar_t* szFile) {
    std::ifstream file(szFile, std::ios::binary | std::ios::ate);
    if (!file) return false;

    std::streamsize size = file.tellg();
    file.seekg(0, std::ios::beg);
    std::vector<char> buffer(size);
    if (file.read(buffer.data(), size)) {
        int wlen = MultiByteToWideChar(CP_UTF8, 0, buffer.data(), (int)size, NULL, 0);
        std::wstring wstr(wlen, 0);
        MultiByteToWideChar(CP_UTF8, 0, buffer.data(), (int)size, &wstr[0], wlen);

        SetWindowTextW(hEdit, wstr.c_str());
        wcscpy_s(g_currentFilePath, MAX_PATH, szFile);
        HighlightSyntax(hEdit);
        return true;
    }
    return false;
}

void OpenFile(HWND hwnd, HWND hEdit) {
    OPENFILENAMEW ofn;
    wchar_t szFile[MAX_PATH] = { 0 };

    ZeroMemory(&ofn, sizeof(ofn));
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = hwnd;
    ofn.lpstrFile = szFile;
    ofn.nMaxFile = sizeof(szFile) / sizeof(wchar_t);
    ofn.lpstrFilter = L"Tüm Kod Dosyaları\0*.*\0C/C++ (*.cpp;*.h)\0*.cpp;*.h\0C# (*.cs)\0*.cs\0Python (*.py)\0*.py\0";
    ofn.nFilterIndex = 1;
    ofn.Flags = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST;

    if (GetOpenFileNameW(&ofn)) {
        LoadFileToEdit(hEdit, szFile);
    }
}

bool SaveFileAs(HWND hwnd, HWND hEdit) {
    OPENFILENAMEW ofn;
    wchar_t szFile[MAX_PATH] = { 0 };

    ZeroMemory(&ofn, sizeof(ofn));
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = hwnd;
    ofn.lpstrFile = szFile;
    ofn.nMaxFile = sizeof(szFile) / sizeof(wchar_t);
    ofn.lpstrFilter = L"Tüm Dosyalar (*.*)\0*.*\0C++ (*.cpp)\0*.cpp\0C# (*.cs)\0*.cs\0Python (*.py)\0*.py\0";
    ofn.nFilterIndex = 1;
    ofn.Flags = OFN_PATHMUSTEXIST | OFN_OVERWRITEPROMPT;

    if (GetSaveFileNameW(&ofn)) {
        int length = GetWindowTextLengthW(hEdit);
        std::vector<wchar_t> wbuf(length + 1);
        GetWindowTextW(hEdit, wbuf.data(), length + 1);

        int u8len = WideCharToMultiByte(CP_UTF8, 0, wbuf.data(), length, NULL, 0, NULL, NULL);
        std::string u8str(u8len, 0);
        WideCharToMultiByte(CP_UTF8, 0, wbuf.data(), length, &u8str[0], u8len, NULL, NULL);

        std::ofstream file(szFile, std::ios::binary);
        if (file) {
            file.write(u8str.c_str(), u8len);
            wcscpy_s(g_currentFilePath, MAX_PATH, szFile);
            return true;
        }
    }
    return false;
}

void SaveFile(HWND hwnd, HWND hEdit) {
    if (g_currentFilePath[0] == L'\0') {
        SaveFileAs(hwnd, hEdit);
    } else {
        int length = GetWindowTextLengthW(hEdit);
        std::vector<wchar_t> wbuf(length + 1);
        GetWindowTextW(hEdit, wbuf.data(), length + 1);

        int u8len = WideCharToMultiByte(CP_UTF8, 0, wbuf.data(), length, NULL, 0, NULL, NULL);
        std::string u8str(u8len, 0);
        WideCharToMultiByte(CP_UTF8, 0, wbuf.data(), length, &u8str[0], u8len, NULL, NULL);

        std::ofstream file(g_currentFilePath, std::ios::binary);
        if (file) {
            file.write(u8str.c_str(), u8len);
        }
    }
}

LRESULT CALLBACK EditSubclassProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    if (msg == WM_KEYDOWN) {
        if (wParam == VK_F5) {
            SaveFile(g_hwndMain, g_hEdit);
            RunCode(g_hwndMain, g_currentFilePath);
            return 0;
        }
        if ((GetKeyState(VK_CONTROL) & 0x8000) && (wParam == 'A' || wParam == 'a')) {
            SendMessage(hwnd, EM_SETSEL, 0, -1);
            return 0;
        }
        if ((GetKeyState(VK_CONTROL) & 0x8000) && (wParam == 'S' || wParam == 's')) {
            SaveFile(g_hwndMain, g_hEdit);
            return 0;
        }
        if ((GetKeyState(VK_CONTROL) & 0x8000) && (wParam == 'N' || wParam == 'n')) {
            SetWindowTextW(hwnd, L"");
            g_currentFilePath[0] = L'\0';
            return 0;
        }
    }

    LRESULT result = CallWindowProc(oldEditProc, hwnd, msg, wParam, lParam);

    if (msg == WM_KEYUP) {
        if (wParam == VK_SPACE || wParam == VK_RETURN || wParam == VK_BACK) {
            HighlightSyntax(hwnd);
        }
    }

    return result;
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
        case WM_CREATE: {
            g_hwndMain = hwnd;

            // Windows Sürükle-Bırak Desteğini Aç
            DragAcceptFiles(hwnd, TRUE);

            // Windows 11/10 Koyu Başlık Çubuğu
            BOOL useDarkMode = TRUE;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &useDarkMode, sizeof(useDarkMode));

            HMENU hMenuBar = CreateMenu();
            HMENU hFileMenu = CreatePopupMenu();
            HMENU hRunMenu = CreatePopupMenu();

            AppendMenuW(hFileMenu, MF_STRING, ID_FILE_NEW,    L"Yeni\tCtrl+N");
            AppendMenuW(hFileMenu, MF_STRING, ID_FILE_OPEN,   L"Aç...\tCtrl+O");
            AppendMenuW(hFileMenu, MF_STRING, ID_FILE_SAVE,   L"Kaydet\tCtrl+S");
            AppendMenuW(hFileMenu, MF_STRING, ID_FILE_SAVEAS, L"Farklı Kaydet...");
            AppendMenuW(hFileMenu, MF_SEPARATOR, 0, NULL);
            AppendMenuW(hFileMenu, MF_STRING, ID_FILE_EXIT,   L"Çıkış");

            AppendMenuW(hRunMenu, MF_STRING, ID_CODE_RUN,     L"Çalıştır\tF5");

            AppendMenuW(hMenuBar, MF_POPUP, (UINT_PTR)hFileMenu, L"Dosya");
            AppendMenuW(hMenuBar, MF_POPUP, (UINT_PTR)hRunMenu,  L"Çalıştır");
            SetMenu(hwnd, hMenuBar);

            g_hEdit = CreateWindowExW(
                0, MSFTEDIT_CLASS, L"",
                WS_CHILD | WS_VISIBLE | WS_VSCROLL | WS_HSCROLL | 
                ES_MULTILINE | ES_AUTOVSCROLL | ES_AUTOHSCROLL | ES_NOHIDESEL | ES_WANTRETURN,
                0, 0, 0, 0,
                hwnd, (HMENU)IDC_MAIN_EDIT, GetModuleHandle(NULL), NULL
            );

            oldEditProc = (WNDPROC)SetWindowLongPtr(g_hEdit, GWLP_WNDPROC, (LONG_PTR)EditSubclassProc);
            SendMessage(g_hEdit, EM_SETBKGNDCOLOR, 0, (LPARAM)bgEditColor);

            CHARFORMAT2W cf = { sizeof(CHARFORMAT2W) };
            cf.cbSize = sizeof(CHARFORMAT2W);
            cf.dwMask = CFM_COLOR | CFM_FACE | CFM_SIZE | CFM_CHARSET;
            cf.crTextColor = fgEditColor;
            cf.yHeight = 18 * 20; 
            cf.bCharSet = TURKISH_CHARSET;
            lstrcpyW(cf.szFaceName, L"Consolas");
            SendMessage(g_hEdit, EM_SETCHARFORMAT, SCF_ALL, (LPARAM)&cf);

            // Eğer "Birlikte Aç" ile bir dosya yolu geldiyse aç
            if (g_initialFilePath[0] != L'\0') {
                LoadFileToEdit(g_hEdit, g_initialFilePath);
            }
            break;
        }

        // Dosya Sürüklenip Bırakıldığında
        case WM_DROPFILES: {
            HDROP hDrop = (HDROP)wParam;
            wchar_t szDroppedFile[MAX_PATH] = { 0 };
            if (DragQueryFileW(hDrop, 0, szDroppedFile, MAX_PATH)) {
                LoadFileToEdit(g_hEdit, szDroppedFile);
            }
            DragFinish(hDrop);
            break;
        }

        case WM_COMMAND: {
            int wmId = LOWORD(wParam);
            switch (wmId) {
                case ID_FILE_NEW:
                    SetWindowTextW(g_hEdit, L"");
                    g_currentFilePath[0] = L'\0';
                    break;
                case ID_FILE_OPEN:
                    OpenFile(hwnd, g_hEdit);
                    break;
                case ID_FILE_SAVE:
                    SaveFile(hwnd, g_hEdit);
                    break;
                case ID_FILE_SAVEAS:
                    SaveFileAs(hwnd, g_hEdit);
                    break;
                case ID_FILE_EXIT:
                    DestroyWindow(hwnd);
                    break;
                case ID_CODE_RUN:
                    SaveFile(hwnd, g_hEdit);
                    RunCode(hwnd, g_currentFilePath);
                    break;
            }
            break;
        }

        case WM_SIZE: {
            UINT width = LOWORD(lParam);
            UINT height = HIWORD(lParam);
            MoveWindow(g_hEdit, 0, 0, width, height, TRUE);
            break;
        }

        case WM_DESTROY:
            PostQuitMessage(0);
            break;

        default:
            return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
    return 0;
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PWSTR lpCmdLine, int nCmdShow) {
    // Komut Satırı Argümanlarını (Birlikte Aç yolunu) Yakala
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argv && argc > 1) {
        wcscpy_s(g_initialFilePath, MAX_PATH, argv[1]);
    }
    if (argv) LocalFree(argv);

    HMODULE hRichEdit = LoadLibraryW(L"msftedit.dll");
    if (!hRichEdit) return 0;

    const wchar_t CLASS_NAME[] = L"OptivonEditorClass";

    WNDCLASSEXW wc = { sizeof(WNDCLASSEXW) };
    wc.cbSize        = sizeof(WNDCLASSEXW);
    wc.lpfnWndProc   = WndProc;
    wc.hInstance     = hInstance;
    wc.lpszClassName = CLASS_NAME;
    wc.hCursor       = LoadCursor(NULL, IDC_ARROW);
    wc.hbrBackground = CreateSolidBrush(RGB(30, 30, 30));

    if (!RegisterClassExW(&wc)) return 0;

    HWND hwnd = CreateWindowExW(
        0, CLASS_NAME, L"Optivon Code Editor",
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, 900, 650,
        NULL, NULL, hInstance, NULL
    );

    if (hwnd == NULL) return 0;

    ShowWindow(hwnd, nCmdShow);
    UpdateWindow(hwnd);

    MSG msg = {};
    while (GetMessageW(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    FreeLibrary(hRichEdit);
    return 0;
}