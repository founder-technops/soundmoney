document.addEventListener('DOMContentLoaded', function () {
    const checkboxes = document.querySelectorAll('.score-checkbox');
    const dropdownLabel = document.getElementById('scoreDropdownLabel');

    if (checkboxes.length && dropdownLabel) {
        checkboxes.forEach(cb => {
            cb.addEventListener('change', function () {
                const checked = Array.from(checkboxes).filter(c => c.checked).map(c => c.value);
                if (checked.length === 0) {
                    dropdownLabel.textContent = 'All scores';
                } else if (checked.length <= 2) {
                    dropdownLabel.textContent = checked.join(', ');
                } else {
                    dropdownLabel.textContent = `${checked.length} Scores Selected`;
                }
            });
        });
    }
});