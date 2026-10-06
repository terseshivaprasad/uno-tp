// The words of every validation and error a script shows. They are written once,
// in UnoTP.Data/Messages.cs. The layout puts the ones the scripts use on the page,
// and a script asks for one by its page and name:
//   message('Lists.FromAfterTo')
// A message with a value in it carries the value's name in braces, and is given
// the value by that name:
//   message('BankDetails.NoBranchMatches', { typed: 'hdfc' })
(function () {
  var holder = document.getElementById('appMessages');
  var words = holder ? JSON.parse(holder.textContent) : {};

  window.message = function (name, values) {
    var text = words[name];
    if (text === undefined) return name;
    if (!values) return text;
    return text.replace(/\{(\w+)\}/g, function (whole, key) {
      return values[key] === undefined ? whole : values[key];
    });
  };
})();
