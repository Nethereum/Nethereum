function TestNetChanged(selector) {
    window.location.href = "/TestNet/" + $(selector).val();
}

$("#ballanceButton").click(function() {
    var endpoint = $("#endpoint").text();
    var account = $("#account").val();

    $.ajax({
            type: "POST",
            url: "/api/Ballance",
            data: { EndpointUrl: endpoint, AccountAddress: account }
        })
        .done(function(result) {
            $("#ballanceLabel").text(result);
        })
        .fail(function() {
            $("#ballanceLabel").text("Cannot recieve data");
        })
        .always(function () {
            $("#ballanceResult").removeClass("invisible");
        });
}); 

$("#transferButton").click(function() {
    var endpoint = $("#endpoint").text();
    var privateKey = $("#privatekey").val();
    var to = $("#recipientAccount").val();
    var amount = $("#amount").val();

    $.ajax({
            type: "POST",
            url: "/api/Transfer",
            data: { EndpointUrl: endpoint, To: to, PrivateKey: privateKey, Amount: amount }
        })
        .done(function(result) {
            $("#transferLabel").text(result);
        })
        .fail(function() {
            $("#transferLabel").text("Cannot recieve data");
        })
        .always(function () {
            $("#transferResult").removeClass("invisible");
        });
});