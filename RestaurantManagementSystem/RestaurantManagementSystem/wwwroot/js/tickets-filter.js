(function (global, $) {
  'use strict';

  function initTicketsTableWithStatusFilter(tableSelector, filterSelector, dataTableOptions) {
    if (!$ || !$.fn || !$.fn.DataTable) {
      console.warn('DataTables not found; skipping table initialization');
      return null;
    }
    var opts = $.extend(true, {
      order: [[5, 'desc']],
      pageLength: 25,
      columnDefs: [{ orderable: false, targets: 7 }]
    }, dataTableOptions || {});

    var dt = $(tableSelector).DataTable(opts);

    // Bind quick client-side status filter
    var $filter = $(filterSelector);
    if ($filter.length) {
      $filter.on('change', function () {
        var val = $(this).val();
        if (!val) {
          dt.column(4).search('').draw();
        } else {
          var escaped = $.fn.dataTable.util.escapeRegex(val);
          // Exact word match within the Status cell text
          dt.column(4).search('\\b' + escaped + '\\b', true, false).draw();
        }
      });
    }

    return dt;
  }

  // Expose to window
  global.TicketsFilter = {
    init: initTicketsTableWithStatusFilter
  };

})(window, window.jQuery);
