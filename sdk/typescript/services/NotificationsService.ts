/* generated using openapi-typescript-codegen -- do no edit */
/* istanbul ignore file */
/* tslint:disable */
/* eslint-disable */
import type { NotificationListOrder } from '../models/NotificationListOrder';
import type { NotificationStatus } from '../models/NotificationStatus';
import type { NotificationType } from '../models/NotificationType';

import type { CancelablePromise } from '../core/CancelablePromise';
import { OpenAPI } from '../core/OpenAPI';
import { request as __request } from '../core/request';

export class NotificationsService {

    /**
     * @returns any Success
     * @throws ApiError
     */
    public static getV3Notifications({
        id,
        includeStatistics = false,
    }: {
        id: number,
        includeStatistics?: boolean,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/v3/notifications/{id}',
            path: {
                'id': id,
            },
            query: {
                'includeStatistics': includeStatistics,
            },
        });
    }

    /**
     * @returns any Success
     * @throws ApiError
     */
    public static getV3Notifications1({
        eventId,
        productId,
        status,
        type,
        recipientUserId,
        order,
        desc,
        includeStatistics,
        page,
        count,
        limit,
        offset,
    }: {
        eventId?: number,
        productId?: number,
        status?: NotificationStatus,
        type?: NotificationType,
        recipientUserId?: string,
        order?: NotificationListOrder,
        desc?: boolean,
        /**
         * Whether to include delivery statistics into response.
         */
        includeStatistics?: boolean,
        page?: number,
        count?: number,
        limit?: number,
        offset?: number,
    }): CancelablePromise<any> {
        return __request(OpenAPI, {
            method: 'GET',
            url: '/v3/notifications',
            query: {
                'EventId': eventId,
                'ProductId': productId,
                'Status': status,
                'Type': type,
                'RecipientUserId': recipientUserId,
                'Order': order,
                'Desc': desc,
                'IncludeStatistics': includeStatistics,
                'Page': page,
                'Count': count,
                'Limit': limit,
                'Offset': offset,
            },
        });
    }

}
