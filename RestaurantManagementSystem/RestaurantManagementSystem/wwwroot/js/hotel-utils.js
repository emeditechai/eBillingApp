/**
 * Hotel Booking Module - Shared Utilities
 * Contains shared date validation and form utilities for the hotel module
 */

var HotelUtils = {
    /**
     * Update checkout date minimum when checkin date changes
     * @param {string} checkInSelector - jQuery selector for check-in date input
     * @param {string} checkOutSelector - jQuery selector for check-out date input
     */
    setupDateValidation: function(checkInSelector, checkOutSelector) {
        $(checkInSelector).change(function() {
            var checkInValue = $(this).val();
            if (!checkInValue) return;
            
            var checkIn = new Date(checkInValue);
            checkIn.setDate(checkIn.getDate() + 1);
            var minCheckout = checkIn.toISOString().split('T')[0];
            
            $(checkOutSelector).attr('min', minCheckout);
            
            // Update checkout if current value is before new minimum
            if ($(checkOutSelector).val() < minCheckout) {
                $(checkOutSelector).val(minCheckout);
            }
        });
    },

    /**
     * Calculate number of nights between two dates
     * @param {Date} checkIn - Check-in date
     * @param {Date} checkOut - Check-out date
     * @returns {number} Number of nights
     */
    calculateNights: function(checkIn, checkOut) {
        if (!checkIn || !checkOut || checkOut <= checkIn) {
            return 0;
        }
        return Math.ceil((checkOut - checkIn) / (1000 * 60 * 60 * 24));
    },

    /**
     * Format currency in Indian Rupees
     * @param {number} amount - Amount to format
     * @returns {string} Formatted currency string
     */
    formatCurrency: function(amount) {
        return '₹' + amount.toFixed(2);
    },

    /**
     * Calculate booking price with tax
     * @param {number} roomRate - Room rate per night
     * @param {number} nights - Number of nights
     * @param {number} taxRate - Tax rate (default 0.18 for 18% GST)
     * @returns {object} Object with subtotal, tax, and total
     */
    calculateBookingPrice: function(roomRate, nights, taxRate) {
        taxRate = taxRate || 0.18;
        var subtotal = roomRate * nights;
        var tax = subtotal * taxRate;
        var total = subtotal + tax;
        
        return {
            subtotal: subtotal,
            tax: tax,
            total: total
        };
    },

    /**
     * Parse date from input field value
     * @param {string} dateValue - Date string from input
     * @returns {Date|null} Parsed date or null if invalid
     */
    parseDate: function(dateValue) {
        if (!dateValue) return null;
        var date = new Date(dateValue);
        return isNaN(date.getTime()) ? null : date;
    },

    /**
     * Format date for display
     * @param {Date} date - Date to format
     * @param {string} format - Format string (default 'dd MMM yyyy')
     * @returns {string} Formatted date string
     */
    formatDate: function(date, format) {
        if (!date) return '';
        
        var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 
                      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        
        var day = date.getDate().toString().padStart(2, '0');
        var month = months[date.getMonth()];
        var year = date.getFullYear();
        
        return day + ' ' + month + ' ' + year;
    }
};
