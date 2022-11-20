window.ScrollToBottom = (elementName, force) => {
    //console.log('ScrollToBottom', elementName, force);
    element = document.getElementById(elementName);
    if (!element)
        return false;
    
    var distanceFromBottom = element.scrollHeight - (element.scrollTop + element.offsetHeight);
    if (distanceFromBottom < 30 || force) {
        element.scrollTop = element.scrollHeight;
        return true;
    } else {
        return false;
    }
}
