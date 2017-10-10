// wait for dom
$(function () {

    // dragable
    if ($('html').hasClass("touch")) {
        var useHandle = 'img';
    } else {
        var useHandle = 0;
    }

    $(".draggable").draggable({
        axis: "x",
        handle: useHandle,
        containment: 'parent'
    });

    // reset gif
    $('.reset-anim').each(function () {
        var curSrc = $(this).attr("src");
        $(this).attr("src", curSrc + '?' + Math.random());
    });

});

$(document).ready(function () {

    var scroll_start = 0;
    var hero_image = $('.campaign-hero-image');
    var offset = hero_image.offset();

    $(document).scroll(function () {
        scroll_start = $(this).scrollTop();
        if (scroll_start > offset.top) {
            hero_image.addClass('scroll-down');
        } else {
            hero_image.removeClass('scroll-down');
        }

    });

    $("#draggable").draggable();
});
