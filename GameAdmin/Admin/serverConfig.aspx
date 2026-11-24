<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" %>
<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style type="text/css">
        .configTable { border-collapse: collapse; margin: 15px 0; width: 700px; }
        .configTable td { border-bottom: 1px solid #C4C4C4; padding: 8px 10px; }
        .configTable td.check { width: 30px; text-align: center; }
        .configTable .key { color: #888888; font-size: 11px; }
        #configMsg { min-height: 20px; font-weight: bold; }
        #configMsg.ok { color: #006600; }
        #configMsg.error { color: #CC0000; }
    </style>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Cấu hình trò chơi</h2>
    <p>Bật hoặc tắt từng tùy chọn. Thay đổi được lưu ngay và áp dụng cho máy chủ đang chạy.</p>
    <table class="configTable">
        <tr>
            <td class="check"><input type="checkbox" id="CardResetAlwaysMax" class="gameOption" disabled="disabled" /></td>
            <td><label for="CardResetAlwaysMax">Tẩy điểm thẻ bài luôn ra chỉ số cao nhất</label><br /><span class="key">CardResetAlwaysMax</span></td>
        </tr>
        <tr>
            <td class="check"><input type="checkbox" id="TexpNoDailyLimit" class="gameOption" disabled="disabled" /></td>
            <td><label for="TexpNoDailyLimit">Tu luyện không giới hạn số lần mỗi ngày</label><br /><span class="key">TexpNoDailyLimit</span></td>
        </tr>
        <tr>
            <td class="check"><input type="checkbox" id="ItemAdvanceAlwaysSucceed" class="gameOption" disabled="disabled" /></td>
            <td><label for="ItemAdvanceAlwaysSucceed">Tăng bậc vật phẩm bằng Đá tăng bậc luôn thành công</label><br /><span class="key">ItemAdvanceAlwaysSucceed</span></td>
        </tr>
        <tr>
            <td class="check"><input type="checkbox" id="WishBeadAlwaysSucceed" class="gameOption" disabled="disabled" /></td>
            <td><label for="WishBeadAlwaysSucceed">Dát vàng bằng Châu báu chúc phúc luôn thành công</label><br /><span class="key">WishBeadAlwaysSucceed</span></td>
        </tr>
        <tr>
            <td class="check"><input type="checkbox" id="StrengthenAlwaysSucceed" class="gameOption" disabled="disabled" /></td>
            <td><label for="StrengthenAlwaysSucceed">Cường hóa luôn thành công</label><br /><span class="key">StrengthenAlwaysSucceed</span></td>
        </tr>
    </table>
    <div id="configMsg"></div>
    <script type="text/javascript">
        $(function () {
            var $j = jQuery;
            var msg = $j("#configMsg");
            function show(text, ok) {
                msg.text(text).attr("class", ok ? "ok" : "error");
            }
            $j.ajax({
                type: "POST",
                url: "mainRequest.ashx/GetGameOptions",
                data: "{}",
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                success: function (result) {
                    $j.each(result.d, function (name, on) {
                        $j("#" + name).prop("checked", on).prop("disabled", false);
                    });
                },
                error: function () {
                    show("Không tải được cấu hình.", false);
                }
            });
            $j(".gameOption").change(function () {
                var box = $j(this);
                var on = box.is(":checked");
                box.prop("disabled", true);
                $j.ajax({
                    type: "POST",
                    url: "mainRequest.ashx/SetGameOption",
                    data: JSON.stringify({ Name: box.attr("id"), Value: on }),
                    contentType: "application/json; charset=utf-8",
                    dataType: "json",
                    success: function (result) {
                        if (!result.d.Saved) {
                            box.prop("checked", !on);
                            show("Lưu thất bại!", false);
                        } else if (result.d.Reloaded) {
                            show("Đã lưu và áp dụng cho máy chủ.", true);
                        } else {
                            show("Đã lưu. Không kết nối được máy chủ trung tâm: thay đổi sẽ áp dụng khi khởi động lại máy chủ game.", true);
                        }
                    },
                    error: function () {
                        box.prop("checked", !on);
                        show("Lưu thất bại!", false);
                    },
                    complete: function () {
                        box.prop("disabled", false);
                    }
                });
            });
        });
    </script>
</asp:Content>
