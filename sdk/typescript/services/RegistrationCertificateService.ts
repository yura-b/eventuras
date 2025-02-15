/* generated using openapi-typescript-codegen -- do no edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';

export class RegistrationCertificateService {

    /**
     * @returns any Success
     * @throws ApiError
     */
    public static postV3RegistrationsCertificateSend({
        id,
    }: {
        id: number,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/v3/registrations/{id}/certificate/send',
            path: {
                'id': id,
            },
        });
    }

}
