import { TranslateHttpLoader } from '@ngx-translate/http-loader';
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ModuleApiService } from "app/core/module-api.service";
import { $2sxcService } from "app/core/$2sxc.service";
import { Http, HttpModule, XHRBackend, RequestOptions } from '@angular/http';
import { Http2sxc } from "app/core/http-interceptor.service";
import { Http2SxcHttpProvider } from "app/core/http-interceptor.service.provider";

@NgModule({
  imports: [
    CommonModule,
    HttpModule
  ],
  declarations: [],
  providers: [
    ModuleApiService,
    $2sxcService,
    Http2SxcHttpProvider,
  ]
})
export class CoreModule { }