// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function validate() {
    var valid = true;

    var date = document.createForm.PlanedFinishDate;

    if (date >= new Date)
    {
        alert("Planed finish date must be greater that current datetime");
        valid = false;
    }

    return valid;
}