var PFM = (function () {
  var RAPID_HOST = 'currency-conversion-and-exchange-rates.p.rapidapi.com';
  var RAPID_KEY  = '17ac984572msh4dc66bb06822766p1257f5jsn16297f76c636';
  var API_BASE   = 'http://localhost:5000/api';

  /* ── Auth helpers ── */
  function getAuthHeaders() {
    var token = localStorage.getItem('pfmJwt');
    return token ? { 'Authorization': 'Bearer ' + token } : {};
  }

  function getCurrentUser() {
    var token = localStorage.getItem('pfmJwt');
    if (!token) return null;
    try {
      var payload = JSON.parse(atob(token.split('.')[1]));
      if (payload.exp * 1000 < Date.now()) {
        localStorage.removeItem('pfmJwt');
        return null;
      }
      return { fullName: payload.fullName, username: payload.username, email: payload.email };
    } catch (e) { return null; }
  }

  function saveCurrentUser(token) {
    localStorage.setItem('pfmJwt', token);
  }

  function logout() {
    localStorage.removeItem('pfmJwt');
  }

  function requireLogin() {
    if (!getCurrentUser()) window.location.href = 'authentication.html';
  }

  /* ── Profile ── */
  function getProfile() {
    return $.ajax({ url: API_BASE + '/profile', method: 'GET', headers: getAuthHeaders() });
  }

  /* ── Currency ── */
  function roundCurrency(value) {
    return Math.round(Number(value || 0) * 100) / 100;
  }

  function getCurrencyMap() {
    return {
      EUR: { symbol: '€', api: 'EUR', name: 'Euro' },
      USD: { symbol: '$', api: 'USD', name: 'US Dollar' },
      LEK: { symbol: 'L', api: 'ALL', name: 'Albanian Lek' }
    };
  }

  function formatAmount(amount, currency) {
    var map = getCurrencyMap();
    var sym = (map[currency || 'EUR'] || map.EUR).symbol;
    return sym + Number(amount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  function getApiDate(dateString) {
    if (dateString) return dateString;
    var d = new Date();
    d.setDate(d.getDate() - 1);
    return d.toISOString().split('T')[0];
  }

  function convertAmount(amount, fromCurrency, toCurrency, dateString) {
    var map      = getCurrencyMap();
    var fromCode = (map[fromCurrency] || map.EUR).api;
    var toCode   = (map[toCurrency]   || map.EUR).api;

    if (fromCode === toCode) {
      return Promise.resolve({ rate: 1, amount: Number(amount), from: fromCurrency, to: toCurrency });
    }

    var date = getApiDate(dateString);
    var url  = 'https://' + RAPID_HOST + '/timeseries'
             + '?start_date=' + date
             + '&end_date='   + date
             + '&base='       + fromCode
             + '&symbols='    + toCode;

    return new Promise(function (resolve, reject) {
      $.ajax({
        url: url, method: 'GET',
        headers: { 'Content-Type': 'application/json', 'x-rapidapi-host': RAPID_HOST, 'x-rapidapi-key': RAPID_KEY }
      }).done(function (data) {
        var rates = data.rates && data.rates[date];
        var rate  = rates ? rates[toCode] : null;
        if (!rate) {
          reject(new Error('No rate returned for ' + fromCode + '→' + toCode + ' on ' + date + '. Response: ' + JSON.stringify(data)));
          return;
        }
        resolve({ rate: Number(rate), amount: Number(amount) * Number(rate), date: date, from: fromCurrency, to: toCurrency });
      }).fail(function (xhr) {
        var msg = (xhr.responseJSON && xhr.responseJSON.message) || xhr.statusText || 'Request failed';
        reject(new Error('API error (' + xhr.status + '): ' + msg));
      });
    });
  }

  /* Calls RapidAPI for the rate, then sends it to the backend which applies the
     conversion atomically across all transactions and the user's savings fields. */
  function convertUserFinancialData(fromCurrency, toCurrency, dateString) {
    return convertAmount(1, fromCurrency, toCurrency, dateString).then(function (result) {
      var rate = Number(result.rate || 1);
      return $.ajax({
        url: API_BASE + '/profile/convert-currency',
        method: 'POST',
        contentType: 'application/json',
        headers: getAuthHeaders(),
        data: JSON.stringify({ fromCurrency: fromCurrency, toCurrency: toCurrency, rate: rate })
      }).then(function (profile) {
        return { rate: rate, date: result.date, from: fromCurrency, to: toCurrency, profile: profile };
      });
    });
  }

  /* ── Validation ── */
  function validateEmail(e)    { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e); }
  function validateUsername(u) { return /^[a-zA-Z0-9_]{4,20}$/.test(u); }
  function validatePassword(p) { return /^(?=.*[A-Za-z])(?=.*\d).{8,}$/.test(p); }
  function getValidationMessages() {
    return {
      email:    'Enter a valid email address such as name@example.com.',
      username: 'Username must be 4–20 characters: letters, numbers, or underscore.',
      password: 'Password must be at least 8 characters with a letter and a number.'
    };
  }

  /* ── Categories ── */
  function getCategories() {
    return {
      Income:  ['Allowance', 'Grants', 'Scholarships', 'Salary', 'Freelance', 'Gift', 'Investment', 'Other Income'],
      Expense: ['Groceries', 'Entertainment', 'Utilities', 'Tuition', 'Transport', 'Housing', 'Health', 'Dining', 'Other Expense']
    };
  }

  function fillCategoryOptions(typeSelector, categorySelector, selected) {
    var type = $(typeSelector).val();
    var list = getCategories()[type] || [];
    var html = '<option value="">Select category</option>';
    list.forEach(function (item) {
      html += '<option value="' + item + '"' + (selected === item ? ' selected' : '') + '>' + item + '</option>';
    });
    $(categorySelector).html(html);
  }

  /* ── UI helpers ── */
  function showMessage(selector, type, message) {
    $(selector).html('<div class="alert alert-' + type + ' mb-0">' + message + '</div>');
  }

  function getStatusBadge(status) {
    return '<span class="badge badge-' + (status || '').toLowerCase() + '">' + (status || '') + '</span>';
  }

  function getInitials() {
    var cur = getCurrentUser();
    return cur ? (cur.username || cur.fullName || cur.email || 'U').charAt(0).toUpperCase() : 'U';
  }

  function getArticleOfDay() {
    var list = [
      { title: 'Review your spending weekly',   text: 'A short weekly review helps you catch unnecessary spending before it becomes a habit.' },
      { title: 'Save first, then spend',         text: 'Move a small amount to savings first. This makes your progress visible and consistent.' },
      { title: 'Use categories carefully',       text: 'When categories are clear and consistent, your reports become much more useful.' },
      { title: 'Check trends, not just totals',  text: 'Income and expenses are easier to manage when you compare periods instead of looking at one number.' }
    ];
    return list[new Date().getDate() % list.length];
  }

  /* currency is passed explicitly because getProfile() is now async */
  function transactionRow(item, currency) {
    var isIncome = item.type === 'Income';
    return '<tr>' +
      '<td>' + item.date + '</td>' +
      '<td><span class="badge badge-' + (isIncome ? 'income' : 'expense') + '">' + item.type + '</span></td>' +
      '<td>' + item.category + '</td>' +
      '<td>' + item.description + '</td>' +
      '<td class="' + (isIncome ? 'amt-income' : 'amt-expense') + '">' + (isIncome ? '+' : '-') + formatAmount(item.amount, currency) + '</td>' +
      '<td>' + getStatusBadge(item.status) + '</td>' +
      '<td class="text-end">' +
        '<a href="edit-transaction.html?id=' + item.id + '" class="btn btn-outline btn-sm me-2">Edit</a>' +
        '<button class="btn btn-danger-soft btn-sm delete-transaction" data-id="' + item.id + '">Delete</button>' +
      '</td>' +
      '</tr>';
  }

  function renderNav(active) {
    var cur      = getCurrentUser();
    var username = cur ? cur.username : 'user';
    var nav = ['transactions', 'savings', 'reports'].map(function (page) {
      var label = page.charAt(0).toUpperCase() + page.slice(1);
      return '<li><a class="nav-link' + (active === page ? ' active' : '') + '" href="' + page + '.html">' + label + '</a></li>';
    }).join('');

    $('#mainNavContainer').html(
      '<nav class="navbar-app">' +
        '<div class="frame">' +
          '<a class="brand" href="dashboard.html">Personal Finance System</a>' +
          '<ul class="nav-pills d-none d-md-flex">' + nav + '</ul>' +
          '<div class="dropdown" style="margin-left:auto;">' +
            '<button class="avatar-btn" data-bs-toggle="dropdown" aria-expanded="false">' + getInitials() + '</button>' +
            '<ul class="dropdown-menu dropdown-menu-end">' +
              '<li><span class="dropdown-item text-muted" style="font-size:.8rem;cursor:default;">@' + username + '</span></li>' +
              '<li><a class="dropdown-item" href="profile.html">Edit profile</a></li>' +
              '<li><button class="dropdown-item" id="logoutBtn">Log out</button></li>' +
            '</ul>' +
          '</div>' +
        '</div>' +
      '</nav>'
    );
  }

  function queryId() {
    return new URLSearchParams(window.location.search).get('id');
  }

  /* ── Init ── */
  $(function () {
    /* Remove legacy localStorage keys left over from the pre-API version */
    ['pfmUsers', 'pfmCurrentUser', 'pfmProfiles', 'pfmTransactions'].forEach(function (k) {
      localStorage.removeItem(k);
    });

    $(document).on('click', '#logoutBtn', function () {
      logout();
      window.location.href = 'authentication.html';
    });

    $(document).on('click', '.delete-transaction', function () {
      var id = $(this).data('id');
      if (!confirm('Delete this transaction?')) return;
      $.ajax({ url: API_BASE + '/transactions/' + id, method: 'DELETE', headers: getAuthHeaders() })
        .done(function () { window.location.reload(); })
        .fail(function (xhr) {
          alert((xhr.responseJSON && xhr.responseJSON.error) || 'Delete failed.');
        });
    });
  });

  return {
    API_BASE,
    getAuthHeaders, getCurrentUser, saveCurrentUser, logout, requireLogin,
    getProfile,
    formatAmount, roundCurrency, getCurrencyMap,
    convertAmount, convertUserFinancialData,
    validateEmail, validateUsername, validatePassword, getValidationMessages,
    getCategories, fillCategoryOptions,
    showMessage, renderNav, queryId, transactionRow,
    getStatusBadge, getArticleOfDay, getInitials
  };
})();
