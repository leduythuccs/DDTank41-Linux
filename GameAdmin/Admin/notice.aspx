<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" %>
<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <style type="text/css">
        .notice { border: 1px solid #C4C4C4; margin: 15px 0; padding: 10px; width: 700px; }
        .notice table { border-collapse: collapse; }
        .notice th { width: 90px; text-align: left; vertical-align: top; padding: 6px 4px; }
        .notice td { padding: 4px; }
        .notice input.title { width: 580px; float: none; }
        .notice textarea { width: 580px; height: 140px; font-family: Arial; font-size: 13px; }
        .notice .status { margin-left: 10px; font-weight: bold; }
        .notice .status.ok { color: #006600; }
        .notice .status.error { color: #CC0000; }
        .notice .id { color: #888888; font-size: 11px; float: right; }
        #noticeMsg { min-height: 20px; font-weight: bold; color: #CC0000; }
    </style>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Thông báo máy chủ</h2>
    <p>Các thông báo hiện ra khi người chơi đăng nhập, từ ngày bắt đầu đến hết ngày trước ngày kết thúc.
        Người chơi đang trong game sẽ thấy thay đổi ở lần đăng nhập sau.</p>
    <div id="noticeMsg"></div>
    <div id="notices"></div>
    <input type="button" id="addNotice" value="Thêm thông báo" />

    <div id="noticeTemplate" class="notice" style="display: none">
        <span class="id"></span>
        <table>
            <tr><th>Tiêu đề</th><td><input type="text" class="title" maxlength="200" /></td></tr>
            <tr><th>Nội dung</th><td><textarea class="text"></textarea></td></tr>
            <tr><th>Bắt đầu</th><td><input type="date" class="begin" /> &nbsp; <b>Kết thúc</b> <input type="date" class="end" /></td></tr>
            <tr><th></th><td>
                <input type="button" class="save" value="Lưu" />
                <input type="button" class="delete" value="Xóa" />
                <span class="status"></span>
            </td></tr>
        </table>
    </div>

    <script type="text/javascript">
        $(function () {
            var $j = jQuery;

            function call(method, data, done, fail) {
                $j.ajax({
                    type: "POST",
                    url: "mainRequest.ashx/" + method,
                    data: JSON.stringify(data),
                    contentType: "application/json; charset=utf-8",
                    dataType: "json",
                    success: function (result) { done(result.d); },
                    error: fail
                });
            }

            function today(addYears) {
                var d = new Date();
                d.setFullYear(d.getFullYear() + (addYears || 0));
                return d.toISOString().substring(0, 10);
            }

            function status(box, text, ok) {
                box.find(".status").text(text).attr("class", "status " + (ok ? "ok" : "error"));
            }

            function saved(box, result, what) {
                if (!result.Saved) {
                    status(box, what + " thất bại!", false);
                } else if (result.Reloaded) {
                    status(box, "Đã " + what.toLowerCase() + ".", true);
                } else {
                    status(box, "Đã " + what.toLowerCase() + ". Không kết nối được máy chủ trung tâm: thông báo mới hoặc đã xóa sẽ áp dụng khi khởi động lại máy chủ game.", true);
                }
            }

            function addBox(n) {
                var box = $j("#noticeTemplate").clone().removeAttr("id").show();
                box.data("id", n.ID);
                box.find(".id").text(n.ID ? "ID " + n.ID : "Mới");
                box.find(".title").val(n.Title);
                box.find(".text").val(n.Text);
                box.find(".begin").val(n.BeginDate);
                box.find(".end").val(n.EndDate);
                box.find(".save").click(function () {
                    var data = {
                        ID: box.data("id"),
                        Title: box.find(".title").val(),
                        Text: box.find(".text").val(),
                        BeginDate: box.find(".begin").val(),
                        EndDate: box.find(".end").val()
                    };
                    if (!data.Title || !data.BeginDate || !data.EndDate) {
                        status(box, "Cần tiêu đề, ngày bắt đầu và ngày kết thúc.", false);
                        return;
                    }
                    call("SaveEdictum", data, function (result) {
                        saved(box, result, "Lưu");
                        if (result.Saved && !data.ID) {
                            load();  // a new notice gets its ID from the database
                        }
                    }, function () { status(box, "Lưu thất bại!", false); });
                });
                box.find(".delete").click(function () {
                    var id = box.data("id");
                    if (!id) {
                        box.remove();
                        return;
                    }
                    if (!confirm("Xóa thông báo \"" + box.find(".title").val() + "\"?")) {
                        return;
                    }
                    call("DeleteEdictum", { ID: id }, function (result) {
                        if (result.Saved) {
                            box.remove();
                            if (!result.Reloaded) {
                                $j("#noticeMsg").text("Đã xóa. Không kết nối được máy chủ trung tâm: áp dụng khi khởi động lại máy chủ game.");
                            }
                        } else {
                            status(box, "Xóa thất bại!", false);
                        }
                    }, function () { status(box, "Xóa thất bại!", false); });
                });
                $j("#notices").append(box);
            }

            function load() {
                call("GetEdictums", {}, function (list) {
                    $j("#notices").empty();
                    $j.each(list, function (i, n) { addBox(n); });
                }, function () { $j("#noticeMsg").text("Không tải được thông báo."); });
            }

            $j("#addNotice").click(function () {
                addBox({ ID: 0, Title: "", Text: "", BeginDate: today(), EndDate: today(10) });
            });
            load();
        });
    </script>
</asp:Content>
