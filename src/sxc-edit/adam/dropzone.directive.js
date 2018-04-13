﻿/* js/fileAppDirectives */
(function () {
    angular.module('Adam')
        /*@ngInject*/
        .directive('dropzone', function (sxc, tabId, AppInstanceId, ContentBlockId, dragClass, adamSvc, $timeout, $translate) {

            return {
                restrict: 'C',
                link: postLink,

                // This controller is needed, because it needs an API which can talk to other directives
                controller: controller
            };


            // this is the method called after linking the directive, which initializes Dropzone
            function postLink(scope, element, attrs, controller) {
                var header = scope.$parent.to.header;
                var field = scope.$parent.options.key;
                var entityGuid = header.Guid;
                var svc = adamSvc(header.ContentTypeName, entityGuid, field, '', scope.$parent.vm.adamModeConfig);
                var url = svc.url;

                var config = {
                    url: url,
                    urlRoot: url,
                    maxFilesize: 10000, // 10'000 MB = 10 GB, note that it will also be stopped on the server if it's larger than the really allowed sized
                    paramName: 'uploadfile',
                    maxThumbnailFilesize: 10,

                    headers: {
                        "ModuleId": AppInstanceId,
                        "TabId": tabId,
                        "ContentBlockId": ContentBlockId
                    },

                    dictDefaultMessage: '',
                    addRemoveLinks: false,
                    previewsContainer: '.field-' + field.toLowerCase() + ' .dropzone-previews',
                    // we need a clickable, because otherwise the entire area is clickable. so i'm just making the preview clickable, as it's not important
                    clickable: '.field-' + field.toLowerCase() + ' .invisible-clickable' // " .dropzone-adam"
                };


                var eventHandlers = {
                    'addedfile': function (file) {
                        // debugger;
                        $timeout(function () {
                            // anything you want can go here and will safely be run on the next digest.
                            scope.$apply(function () { // this must run in a timeout
                                scope.uploading = true;
                            });
                        });
                    },

                    'drop': function (event) {
                        // debugger;
                        console.log('stv: drop', event);
                    },

                    "processing": function (file) {
                        // debugger;
                        this.options.url = svc.uploadUrl(controller.adam.subFolder);
                    },

                    'success': function (file, response) {
                        // debugger;
                        if (response.Success) {
                            svc.addFullPath(response); // calculate additional infos
                            scope.$parent.afterUpload(response);
                        } else {
                            alert('Upload failed because: ' + response.Error);
                        }
                    },
                    'error': function (file, error, xhr) {
                        alert($translate.instant('Errors.AdamUploadError'));
                    },

                    "queuecomplete": function (file) {
                        if (this.getUploadingFiles().length === 0 && this.getQueuedFiles().length === 0) {
                            scope.uploading = false;
                            controller.adam.refresh();
                        }
                    }
                };

                // delay building the dropszone till the DOM is ready
                $timeout(function () {
                    var dropzone = new Dropzone(element[0], config);

                    angular.forEach(eventHandlers, function (handler, event) {
                        dropzone.on(event, handler);
                    });

                    scope.processDropzone = function () { dropzone.processQueue(); };
                    scope.resetDropzone = function () { dropzone.removeAllFiles(); };
                    controller.openUpload = function () { dropzone.hiddenFileInput.click(); };
                    controller.processFile = function (file) { dropzone.processFile(file); }; // needed for clipboard paste


                    var pasteInstance;

                    // pastableTextarea - for adam input
                    pasteInstance = $(element[0]).children("div:first").children("div.after-preview:first").children("div:first").children("input:first");
                    if (pasteInstance.length > 0) {
                        pasteInstance.pastableTextarea();

                        // pastableNonInputable
                        pasteInstance = $(element[0]); // whole dropzone
                        pasteInstance.pastableNonInputable();

                        pasteInstance.on('pasteImage', function (ev, data) {
                            pasteImageInDropzone(ev, data, dropzone);
                        });
                    }

                    // pastableContenteditable - for tinymce
                    pasteInstance = $(element[0]).children("div:first").children("div[contenteditable]:first");
                    if (pasteInstance.length > 0) {
                        pasteInstance.pastableContenteditable();
                        pasteInstance.on('pasteImage', function (ev, data) {
                            pasteImageInDropzone(ev, data, dropzone);
                        });
                    }

                }, 0);

                function pasteImageInDropzone(ev, data, dropzone) {

                    // todo: generate hash sha256 for file name and avoid duplicate files
                    var imageFileName = 'image.png';
                    imageFileName = window.prompt('Enter clipboard image file name: ', imageFileName); // todo: i18n 

                    // todo: convert png to jpg to reduce file size
                    var img = getFile(data, imageFileName);

                    dropzone.processFile(img);
                }

                /**
                 * creates new file with custom fileName
                 * @param {File} file
                 * @param {string} fileName
                 */
                function getFile(data, fileName) {
                    var newFile = data.file; // for fallback

                    try {
                        if (!document.documentMode && !/Edge/.test(navigator.userAgent)) {
                            // File.name is readonly so we do this
                            var formData = new FormData();
                            formData.append('file', data.file, fileName);
                            newFile = formData.get('file');
                        } else {
                            // fix this for Edge and IE
                            newFile = new Blob([data.file], { type: data.file.type });
                            newFile.lastModifiedDate = data.file.lastModifiedDate;
                            newFile.name = fileName;
                        }
                    } catch (e) {
                        console.log('paste image error', e);
                    }
                    return newFile;
                }


            }



            /*@ngInject*/
            function controller() {
                var vm = this;
                vm.adam = {
                    show: false,
                    subFolder: '',
                    refresh: function () { }
                };

            }

        });


})();