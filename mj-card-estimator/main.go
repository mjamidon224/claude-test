//go:build windows

// Command JordanCardAppraiser opens the appraiser page (index.html, embedded at build
// time) in its own window using the Microsoft Edge WebView2 runtime that ships with
// Windows 10 and 11. Without WebView2 it opens the page in the default browser instead.
package main

import (
	_ "embed"
	"bytes"
	"net/url"
	"os"
	"path/filepath"
	"strings"

	webview2 "github.com/jchv/go-webview2"
	"golang.org/x/sys/windows"
)

//go:embed index.html
var page []byte

const appName = "Jordan Card Appraiser"

// Links in the page open in the real browser instead of a bare WebView2 popup.
const externalLinksJS = `document.addEventListener("click", (e) => {
  const a = e.target.closest && e.target.closest("a[href]");
  if (a && /^https?:/i.test(a.href)) { e.preventDefault(); window.jcaOpenExternal(a.href); }
}, true);`

func main() {
	base := filepath.Join(dataDir(), "JordanCardAppraiser")
	pagePath := filepath.Join(base, "app", "index.html")
	if err := writePage(pagePath); err != nil {
		message("Couldn't save the app files to "+base+".\n\n"+err.Error(), windows.MB_ICONERROR)
		return
	}
	pageURL := (&url.URL{Scheme: "file", Path: "/" + filepath.ToSlash(pagePath)}).String()

	width, height := windowSize()
	w := webview2.NewWithOptions(webview2.WebViewOptions{
		AutoFocus: true,
		DataPath:  filepath.Join(base, "WebView2"),
		WindowOptions: webview2.WindowOptions{
			Title:  appName,
			Width:  width,
			Height: height,
			IconId: 1,
			Center: true,
		},
	})
	if w == nil {
		openExternal(pageURL)
		message("Microsoft Edge WebView2 isn't installed on this PC, so the appraiser opened in your web browser instead.\n\n"+
			"To get the app window, install the WebView2 Runtime from https://developer.microsoft.com/microsoft-edge/webview2/",
			windows.MB_ICONINFORMATION)
		return
	}
	defer w.Destroy()

	_ = w.Bind("jcaOpenExternal", func(link string) {
		if strings.HasPrefix(link, "https://") || strings.HasPrefix(link, "http://") {
			openExternal(link)
		}
	})
	w.Init(externalLinksJS)
	w.Navigate(pageURL)
	w.Run()
}

// writePage saves the page with a doctype so WebView2 renders it in standards mode.
// The file lives at a fixed path so saved prices and settings survive restarts.
func writePage(path string) error {
	content := append([]byte("<!doctype html>\n"), page...)
	if old, err := os.ReadFile(path); err == nil && bytes.Equal(old, content) {
		return nil
	}
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	return os.WriteFile(path, content, 0o644)
}

func dataDir() string {
	if dir, err := os.UserCacheDir(); err == nil {
		return dir
	}
	return os.TempDir()
}

// windowSize picks a window that fits the two-column layout but stays on screen.
func windowSize() (uint, uint) {
	user32 := windows.NewLazySystemDLL("user32.dll")
	metrics := user32.NewProc("GetSystemMetrics")
	screenW, _, _ := metrics.Call(0) // SM_CXSCREEN
	screenH, _, _ := metrics.Call(1) // SM_CYSCREEN
	scale := 1.0
	if dpiForSystem := user32.NewProc("GetDpiForSystem"); dpiForSystem.Find() == nil {
		if dpi, _, _ := dpiForSystem.Call(); dpi > 0 {
			scale = float64(dpi) / 96
		}
	}
	width, height := 1440*scale, 980*scale
	if screenW > 0 {
		width = min(width, 0.94*float64(screenW))
	}
	if screenH > 0 {
		height = min(height, 0.9*float64(screenH))
	}
	return uint(width), uint(height)
}

func openExternal(target string) {
	verb, _ := windows.UTF16PtrFromString("open")
	file, _ := windows.UTF16PtrFromString(target)
	_ = windows.ShellExecute(0, verb, file, nil, nil, windows.SW_SHOWNORMAL)
}

func message(text string, icon uint32) {
	t, _ := windows.UTF16PtrFromString(text)
	c, _ := windows.UTF16PtrFromString(appName)
	_, _ = windows.MessageBox(0, t, c, icon|windows.MB_OK)
}
