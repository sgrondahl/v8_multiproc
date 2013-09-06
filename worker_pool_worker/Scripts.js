function blarble() {
    return sp_request.Request("this is a request from javascript blarble");
}

function blaz() {
    console.WriteLine("javascript: hi");
    return "Hooray!";
}

function err(n) {
    n = n || 0;
    if (n > 3) {
        aoeu;
    }
    err(n + 1);
}