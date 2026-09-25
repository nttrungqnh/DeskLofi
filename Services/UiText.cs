using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DeskLofi.Services;

/// <summary>Static interface text shared by the settings and music windows.</summary>
public static class UiText
{
    private static readonly (string Vi, string En)[] Pairs =
    [
        ("DeskLofi - Cài đặt", "DeskLofi - Settings"),
        ("DeskLofi - Danh sách phát", "DeskLofi - Playlists"),
        ("DeskLofi - Thư viện nhạc", "DeskLofi - Music Library"),
        ("Thư viện nhạc", "Music Library"),
        ("Chung", "General"), ("Cài đặt cơ bản", "Basic settings"),
        ("Thời tiết", "Weather"), ("Hiển thị thời tiết", "Weather display"),
        ("Nhạc", "Music"), ("Nhạc nền và âm lượng", "Music and volume"),
        ("Giao diện", "Appearance"), ("Nhân vật, mèo, đồng hồ", "Character, cat, clock"),
        ("Khác", "Other"), ("Nâng cao", "Advanced"),
        ("Cài đặt cơ bản cho DeskLofi", "Basic settings for DeskLofi"),
        ("🌐   Ngôn ngữ", "🌐   Language"), ("Ngôn ngữ giao diện", "Display language"),
        ("🚀   Khởi động & hiển thị", "🚀   Startup & display"),
        ("Khởi động cùng Windows", "Start with Windows"),
        ("Tự động khởi động cùng Windows", "Launch automatically with Windows"),
        ("Hiển thị trên các ứng dụng khác", "Show over other apps"),
        ("Giữ nhân vật luôn nổi trên cửa sổ ứng dụng khác", "Keep the character above other app windows"),
        ("Hiển thị trên thanh tác vụ", "Show on taskbar"),
        ("Đặt nhân vật trên thanh tác vụ", "Show the character on the taskbar"),
        ("➤   Phản hồi hành động", "➤   Activity response"),
        ("Phản hồi khi gõ phím", "React to keyboard"),
        ("Nhân vật phản hồi khi bạn gõ phím", "The character reacts when you type"),
        ("Phản hồi khi dùng chuột", "React to mouse"),
        ("Nhân vật phản hồi khi bạn dùng chuột", "The character reacts when you use the mouse"),
        ("🐱   Mèo", "🐱   Cat"),
        ("Thời gian mèo ngủ (giây)", "Cat sleep timeout (seconds)"),
        ("Sau thời gian không tương tác, mèo sẽ nằm ngủ", "The cat sleeps after a period of inactivity"),
        ("☕   AFK", "☕   AFK"),
        ("Thời gian chuyển AFK (phút)", "AFK timeout (minutes)"),
        ("Sau thời gian không hoạt động, nhân vật vào trạng thái AFK", "The character enters AFK mode after inactivity"),
        ("▣   Hiển thị", "▣   Display"),
        ("Hiện đồng hồ pixel", "Show pixel clock"),
        ("Hiển thị đồng hồ pixel", "Display the pixel clock"),
        ("Hiện ngày tháng", "Show date"),
        ("Hiển thị ngày tháng", "Display the date"),
        ("☀ ☾   Giao diện tự động", "☀ ☾   Automatic theme"),
        ("Tự động ngày/đêm", "Automatic day/night"),
        ("Tự động chuyển giao diện theo thời gian", "Switch the theme with the time of day"),
        ("☀  06:00  Ban ngày", "☀  06:00  Day"),
        ("☾  18:00  Ban đêm", "☾  18:00  Night"),
        ("Điều chỉnh thời tiết và hiệu ứng trong DeskLofi", "Adjust weather and effects in DeskLofi"),
        ("☀   Thời tiết thực", "☀   Live weather"),
        ("Bật thời tiết thực (Open-Meteo)", "Enable live weather (Open-Meteo)"),
        ("Tên thành phố", "City name"), ("Vĩ độ", "Latitude"), ("Kinh độ", "Longitude"),
        ("Chu kỳ cập nhật (phút)", "Refresh interval (minutes)"),
        ("✦   Hiệu ứng", "✦   Effects"),
        ("Hiệu ứng thời tiết", "Weather effects"),
        ("Âm thanh thời tiết tự động", "Automatic weather ambience"),
        ("Hiệu ứng chớp sáng", "Lightning flashes"),
        ("Âm lượng thời tiết", "Weather volume"),
        ("Thư viện nhạc và âm lượng", "Music library and volume"),
        ("♫   Phát nhạc", "♫   Playback"),
        ("Tự động phát nhạc khi mở app", "Play music when the app opens"),
        ("Âm lượng nhạc", "Music volume"),
        ("▣   Thư viện nhạc", "▣   Music library"),
        ("DeskLofi hỗ trợ MP3 và WAV. Tệp nhạc vẫn ở thư mục gốc của bạn.", "DeskLofi supports MP3 and WAV. Music files stay in their original folders."),
        ("Thêm tệp", "Add files"), ("Thêm thư mục", "Add folder"),
        ("Mở thư viện nhạc", "Open music library"),
        ("Nhân vật và khung cảnh", "Character and scene"),
        ("◉   Nhân vật", "◉   Character"),
        ("Kích thước nhân vật", "Character size"),
        ("Chọn cỡ nhân vật", "Choose character size"),
        ("Nhỏ (75%)", "Small (75%)"), ("Vừa (100%)", "Medium (100%)"),
        ("Lớn (125%)", "Large (125%)"), ("Rất lớn (150%)", "Extra large (150%)"),
        ("Cỡ tối đa (200%)", "Maximum size (200%)"),
        ("Hoặc kéo để chọn cỡ tùy chỉnh", "Or drag to choose a custom size"),
        ("▣   Khung cảnh", "▣   Scene"),
        ("Chọn khung cảnh", "Choose a scene"),
        ("Tùy chọn nâng cao", "Advanced options"),
        ("⚙   Chế độ nhà phát triển", "⚙   Developer mode"),
        ("Mô phỏng thời gian và thời tiết", "Simulate time and weather"),
        ("Thời gian mô phỏng", "Simulated time"),
        ("Thời tiết mô phỏng", "Simulated weather"),
        ("Phòng ngủ", "Bedroom"),
        ("Buổi sáng", "Morning"), ("Ban ngày", "Day"),
        ("Buổi tối", "Evening"), ("Ban đêm", "Night"),
        ("Quang đãng", "Clear"), ("Nhiều mây", "Cloudy"),
        ("Mưa", "Rain"), ("Mưa lớn", "HeavyRain"),
        ("Dông", "Thunderstorm"), ("Sương mù", "Fog"),
        ("Tuyết", "Snow"), ("Mưa phùn", "Drizzle"),
        ("Ít mây", "PartlyCloudy"),
        ("↻  Khôi phục mặc định", "↻  Restore defaults"),
        ("Hủy", "Cancel"), ("▣  Lưu cài đặt", "▣  Save settings"),
        ("Danh sách phát", "Playlists"),
        ("Chọn, tạo và sắp xếp nhạc theo tâm trạng", "Choose and organize music for every mood"),
        ("Tên danh sách phát", "Playlist name"),
        ("+  Tạo mới", "+  Create"), ("Đổi tên", "Rename"), ("Xóa", "Delete"),
        ("Chưa có bài hát", "No tracks yet"),
        ("Thêm nhạc để bắt đầu", "Add music to get started"),
        ("Danh sách này chưa có bài hát.\nNhấn ⚙ để thêm nhạc.", "This playlist has no tracks.\nUse ⚙ to add music."),
        ("☷   Mở danh sách phát", "☷   Open playlists"),
        ("Bài trước", "Previous track"), ("Bài tiếp", "Next track"),
        ("Phát / tạm dừng", "Play / pause"),
        ("Chọn danh sách phát", "Choose playlist"),
        ("Bấm để phát bài hát", "Click to play track"),
        ("Tạo và quản lý danh sách phát", "Create and manage playlists"),
        ("Phát ngẫu nhiên", "Shuffle"), ("Lặp lại", "Repeat"),
        ("Yêu thích", "Favorite"), ("Tùy chọn nhạc", "Music options"),
        ("Xóa tệp lỗi", "Remove missing"),
        ("Thiếu tệp", "MISSING"),
        ("Không có thông tin thời tiết", "Weather unavailable")
    ];

    public static bool IsEnglish(string? language) => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);

    public static string Choose(string? language, string vietnamese, string english) =>
        IsEnglish(language) ? english : vietnamese;

    public static string Translate(string value, string? language)
    {
        foreach (var (vi, en) in Pairs)
            if (value == vi || value == en) return Choose(language, vi, en);
        return value;
    }

    public static void TranslateTree(DependencyObject root, string? language)
    {
        if (root is Window window) window.Title = Translate(window.Title, language);
        if (root is TextBlock text && !BindingOperations.IsDataBound(text, TextBlock.TextProperty))
            text.Text = Translate(text.Text, language);
        if (root is ContentControl content && root is not ListBoxItem && content.Content is string label &&
            !BindingOperations.IsDataBound(content, ContentControl.ContentProperty))
            content.Content = Translate(label, language);
        if (root is FrameworkElement element && element.ToolTip is string tip &&
            !BindingOperations.IsDataBound(element, FrameworkElement.ToolTipProperty))
            element.ToolTip = Translate(tip, language);
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            TranslateTree(child, language);
    }
}
