#ifndef CPP_COMPILER_HPP
#define CPP_COMPILER_HPP

#include <windows.h>

// F5 tuşuna basıldığında çağrılacak ana fonksiyon
void RunCode(HWND hwndOwner, const wchar_t* filePath);

#endif