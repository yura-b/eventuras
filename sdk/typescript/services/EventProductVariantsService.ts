/* generated using openapi-typescript-codegen -- do no edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { NewProductVariantDto } from '../models/NewProductVariantDto';
import type { ProductVariantDto } from '../models/ProductVariantDto';

import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';

export class EventProductVariantsService {

    /**
     * @returns ProductVariantDto Success
     * @throws ApiError
     */
    public static getV3EventsProductsVariants({
        eventId,
        productId,
    }: {
        eventId: number,
        productId: number,
    }): CancelablePromise<Array<ProductVariantDto>> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/v3/events/{eventId}/products/{productId}/variants',
            path: {
                'eventId': eventId,
                'productId': productId,
            },
        });
    }

    /**
     * @returns any Success
     * @throws ApiError
     */
    public static postV3EventsProductsVariants({
        eventId,
        productId,
        requestBody,
    }: {
        eventId: number,
        productId: number,
        requestBody?: NewProductVariantDto,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'POST',
            url: '/v3/events/{eventId}/products/{productId}/variants',
            path: {
                'eventId': eventId,
                'productId': productId,
            },
            body: requestBody,
            mediaType: 'application/json-patch+json',
        });
    }

    /**
     * @returns any Success
     * @throws ApiError
     */
    public static deleteV3EventsProductsVariants({
        eventId,
        productId,
        id,
    }: {
        eventId: number,
        productId: number,
        id: number,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'DELETE',
            url: '/v3/events/{eventId}/products/{productId}/variants/{id}',
            path: {
                'eventId': eventId,
                'productId': productId,
                'id': id,
            },
        });
    }

}
